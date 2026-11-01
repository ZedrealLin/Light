using Dapper;
using System.Text;
using System.Text.Json;
using Light.Api.Common;
using Light.Api.Infrastructure.Auth;
using Light.Api.Infrastructure.Db;
using Light.Api.Repositories;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace Light.Api.Controllers;

/// <summary>当前用户信息（api-spec §4）。默认鉴权（FallbackPolicy），无需再标 [Authorize]。</summary>
[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly UserRepository _users;
    private readonly IDbConnectionFactory _factory;
    private readonly DiaryRepository _diaries;
    private readonly ScaleSubmissionRepository _submissions;
    private readonly AiRepository _ai;
    private readonly Infrastructure.Crypto.IAesCipher _cipher;
    private readonly ILogger<MeController> _logger;

    public MeController(
        UserRepository users, IDbConnectionFactory factory,
        DiaryRepository diaries, ScaleSubmissionRepository submissions,
        AiRepository ai, Infrastructure.Crypto.IAesCipher cipher,
        ILogger<MeController> logger)
    {
        _users = users;
        _factory = factory;
        _diaries = diaries;
        _submissions = submissions;
        _ai = ai;
        _cipher = cipher;
        _logger = logger;
    }

    private long UserId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少用户身份"));

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(UserId, cancellationToken)
            ?? throw new InvalidOperationException("用户不存在");
        var data = new UserBriefResponse(
            user.PublicId, user.Nickname,
            Mask(user.Phone), Mask(user.Email),
            user.PrivacyConsentAt);
        return Ok(data.ToOk(HttpContext));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateMeRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Nickname) && request.Nickname.Length > 64)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, "昵称过长", HttpContext.TraceIdentifier));
        }
        await _users.UpdateProfileAsync(UserId, request.Nickname?.Trim(), cancellationToken);
        return Ok(new { done = true }.ToOk(HttpContext));
    }

    [HttpPost("privacy-consent")]
    public async Task<IActionResult> PrivacyConsent(CancellationToken cancellationToken)
    {
        await _users.RecordPrivacyConsentAsync(UserId, cancellationToken);
        return Ok(new { done = true }.ToOk(HttpContext));
    }

    /// <summary>站内通知列表（最新 100 条；标记已读走 POST /notifications/{id}/read）。</summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<NotificationRaw>(new CommandDefinition(
            "SELECT id, type, title, body, is_read, created_at FROM notifications WHERE user_id = @userId ORDER BY id DESC LIMIT 100",
            new { UserId }, cancellationToken: cancellationToken))).ToList();
        return Ok(new { items = items.Select(r => new
        {
            r.id,
            r.type,
            r.title,
            r.body,
            r.is_read,
            createdAt = r.created_at,
        }).ToList() }.ToOk(HttpContext));
    }

    [HttpPost("notifications/{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE notifications SET is_read = 1 WHERE id = @id AND user_id = @UserId";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { id, UserId }, cancellationToken: cancellationToken));
        return affected > 0
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "通知不存在", HttpContext.TraceIdentifier));
    }

    /// <summary>个人数据导出（硬性规则 4）：资料 + 日记明文 + 作答结果 + AI 对话，JSON 下载。</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(UserId, cancellationToken)
            ?? throw new InvalidOperationException("用户不存在");
        var diaryRows = await _diaries.ListForExportAsync(UserId, cancellationToken);
        var diaries = diaryRows.Select(r => new
        {
            entryDate = r.EntryDate,
            moodScore = r.MoodScore,
            content = _cipher.Decrypt(r.ContentCipher),
            tags = r.TagsJson,
            isCrisis = r.IsCrisis,
            createdAt = r.CreatedAt,
        }).ToList();
        var submissions = (await _submissions.ListForExportAsync(UserId, cancellationToken)).Select(r => new
        {
            scaleCode = r.ScaleCode,
            totalScore = r.TotalScore,
            levelCode = r.LevelCode,
            levelLabel = r.LevelLabel,
            isCrisis = r.IsCrisis,
            createdAt = r.CreatedAt,
        }).ToList();
        var sessions = await _ai.ListSessionsAsync(UserId, cancellationToken);
        var conversations = new List<object>();
        foreach (var (sid, _, _, _) in sessions)
        {
            var msgs = await _ai.ListMessagesAsync(sid, cancellationToken);
            conversations.Add(new
            {
                sessionId = sid,
                messages = msgs.Select(m => new { role = m.Role, content = _cipher.Decrypt(m.ContentCipher), m.CreatedAt }),
            });
        }
        var export = new
        {
            exportedAtUtc = DateTime.UtcNow,
            profile = new { user.PublicId, user.Nickname, user.Phone, user.Email, user.PrivacyConsentAt },
            diaries,
            scaleSubmissions = submissions,
            aiConversations = conversations,
        };
        _logger.LogInformation("个人数据已导出 user={UserId}（审计）", UserId);
        return File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        })), "application/json", $"light-export-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
    }

    /// <summary>彻底删除我的数据与账号（硬性规则 4）：需显式 confirm；事务内按 FK 顺序删除。</summary>
    [HttpPost("delete")]
    [Consumes("application/json")]
    public async Task<IActionResult> DeleteMyData([FromBody] DeleteMeRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirm)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, "请显式确认（confirm=true）后执行删除", HttpContext.TraceIdentifier));
        }
        await _users.RevokeAllRefreshTokensAsync(UserId, cancellationToken);
        await _users.DeleteUserCompletelyAsync(UserId, cancellationToken);
        _logger.LogWarning("用户已彻底删除全部数据并注销 userId={UserId}（审计）", UserId);
        return Ok(new { done = true }.ToOk(HttpContext));
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Contains('@')
            ? $"{value[..2]}****{value[(value.IndexOf('@') - 1)..]}"
            : $"{value[..3]}****{value[^2..]}";
    }

    private sealed class NotificationRaw
    {
        public long id { get; set; }
        public string type { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string? body { get; set; }
        public bool is_read { get; set; }
        public DateTime created_at { get; set; }
    }
}

public sealed record UpdateMeRequest(string? Nickname);

public sealed record DeleteMeRequest(bool Confirm);
