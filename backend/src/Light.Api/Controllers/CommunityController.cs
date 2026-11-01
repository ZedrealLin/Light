using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Light.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>社区集成：DiscourseConnect SSO 回跳 + 社区发帖预警回调（api-spec §3）。</summary>
[ApiController]
[Route("api/community")]
public sealed class CommunityController : ControllerBase
{
    private readonly DiscourseConnectService _sso;
    private readonly UserRepository _users;
    private readonly CrisisDetectionService _detection;
    private readonly CrisisAlertService _crisisAlerts;
    private readonly IConfiguration _configuration;
    private readonly IValidator<CommunityCallbackRequest> _callbackValidator;
    private readonly ILogger<CommunityController> _logger;

    public CommunityController(
        DiscourseConnectService sso, UserRepository users,
        CrisisDetectionService detection, CrisisAlertService crisisAlerts,
        IConfiguration configuration,
        IValidator<CommunityCallbackRequest> callbackValidator,
        ILogger<CommunityController> logger)
    {
        _sso = sso;
        _users = users;
        _detection = detection;
        _crisisAlerts = crisisAlerts;
        _configuration = configuration;
        _callbackValidator = callbackValidator;
        _logger = logger;
    }

    /// <summary>
    /// DiscourseConnect SSO 入口（浏览器 302 落点）：
    /// 本站 JWT 在 localStorage、不随 Discourse 跳转，因此此处只把参数带回前端，
    /// 由已登录的前端页（/community-sso）调 POST /api/community/sso-url 换取最终回跳地址。
    /// </summary>
    [HttpGet("sso")]
    [AllowAnonymous]
    public IActionResult Sso([FromQuery] string? sso, [FromQuery] string? sig)
    {
        if (string.IsNullOrWhiteSpace(sso) || string.IsNullOrWhiteSpace(sig))
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, "缺少 sso/sig 参数", HttpContext.TraceIdentifier));
        }
        return Redirect($"/community-sso?sso={Uri.EscapeDataString(sso)}&sig={Uri.EscapeDataString(sig)}");
    }

    /// <summary>已登录用户换取 Discourse 回跳地址（验证签名 → 组装用户信息 → 返回 302 目标）。</summary>
    [HttpPost("sso-url")]
    [Consumes("application/json")]
    public async Task<IActionResult> SsoUrl([FromBody] SsoInRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var incoming = _sso.VerifyIncoming(request.Sso, request.Sig);
            var user = await _users.FindByIdAsync(UserId, cancellationToken);
            if (user is null)
            {
                return BadRequest(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "账号不存在", HttpContext.TraceIdentifier));
            }
            var (payload, payloadSig) = _sso.BuildResponse(incoming, new DiscourseConnectService.SsoUser(
                user.Id, user.PublicId, user.Email, user.Nickname));
            var redirect = $"{incoming.ReturnSsoUrl}?sso={Uri.EscapeDataString(payload)}&sig={payloadSig}";
            return Ok(new { redirectUrl = redirect }.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    /// <summary>社区发帖回调（匿名 + HMAC 签名头）：命中危机关键词即建工单。</summary>
    [HttpPost("crisis-callback")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> CrisisCallback(CancellationToken cancellationToken)
    {
        // 原始体验签（HMAC-SHA256 over raw body, hex），头名 X-Light-Signature
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        var secret = _configuration["COMMUNITY_CALLBACK_SECRET"]?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "社区回调未配置", HttpContext.TraceIdentifier));
        }
        var provided = Request.Headers["X-Light-Signature"].FirstOrDefault()?.Trim();
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(provided)
            || provided.Length != expected.Length
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided.ToLowerInvariant())))
        {
            _logger.LogWarning("社区回调签名校验失败");
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, "签名校验失败", HttpContext.TraceIdentifier));
        }

        CommunityCallbackRequest? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<CommunityCallbackRequest>(
                rawBody, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException)
        {
            payload = null;
        }
        if (payload is null)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, "回调载荷不合法", HttpContext.TraceIdentifier));
        }
        var validation = await _callbackValidator.ValidateAsync(payload, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }

        var matched = _detection.Match(payload.Content);
        if (matched.Count == 0)
        {
            return Ok(new { alerted = false }.ToOk(HttpContext));
        }

        var externalRef = $"discourse://topic/{payload.TopicId}/post/{payload.PostId}";
        await _crisisAlerts.TryCreateKeywordAlertAsync(
            "community", null, null, null, externalRef, matched, cancellationToken);
        _logger.LogInformation("社区发帖命中危机词已处理 externalRef={ExternalRef}", externalRef);
        return Ok(new { alerted = true }.ToOk(HttpContext));
    }

    private long UserId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少用户身份"));

    public sealed record SsoInRequest(string Sso, string Sig);
}
