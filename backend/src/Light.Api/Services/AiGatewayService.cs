using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Light.Api.Common;
using Light.Api.Infrastructure.Crypto;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Light.Api.Services;

namespace Light.Api.Services;

/// <summary>
/// AI 陪伴网关（阶段 5，PROJECT_BRIEF）：
/// 1. 危机前置拦截：命中关键词 → 返回 7101 + 固定安全话术（CRISIS_SAFE_REPLY 配置），
///    绝不调用模型（硬性规则 2）；
/// 2. 会话限流：每用户每 60 分钟最多 20 条（内存固定窗口；阶段 6 切 Redis，记 TODO）；
/// 3. 审计日志：每次调用记录 user/session/messageId/是否危机（不输出消息明文）；
/// 4. 系统提示词与回复常驻"我不是医生或心理治疗师，不诊断、不建议用药"（硬性规则 6）；
/// 5. 模型调用：AI_API_BASE/AI_API_KEY 配置后走 OpenAI 兼容接口；未配置且 Development → mock 回复。
/// </summary>
public sealed class AiGatewayService
{
    public const string Disclaimer = "我是 AI 陪伴助手，不是医生或心理治疗师；我不能诊断，也不建议用药。如果你正处于危机中，请使用求助资源。";
    public const string CrisisHelpUrl = "/crisis-help";
    private const string FallbackSafeReply =
        "（安全话术占位——上线前人工确认，docs/TODO.md T008）我们注意到你可能正处于艰难的时刻。你的感受很重要，请考虑立即查看求助资源，或告诉身边可信任的人。你现在并不孤单。";

    private const int RateLimitCount = 20;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(60);

    private readonly AiRepository _ai;
    private readonly IAesCipher _cipher;
    private readonly CrisisDetectionService _detection;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiGatewayService> _logger;

    private static readonly ConcurrentDictionary<long, (int Count, DateTime WindowStart)> RateBuckets = new();

    public AiGatewayService(
        AiRepository ai, IAesCipher cipher, CrisisDetectionService detection,
        IConfiguration configuration, IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory, ILogger<AiGatewayService> logger)
    {
        _ai = ai;
        _cipher = cipher;
        _detection = detection;
        _configuration = configuration;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<AiSessionCreatedResponse> CreateSessionAsync(long userId, CancellationToken cancellationToken)
    {
        return CreateSessionInternalAsync(userId, cancellationToken);
    }

    private async Task<AiSessionCreatedResponse> CreateSessionInternalAsync(long userId, CancellationToken cancellationToken)
    {
        var sessionId = await _ai.CreateSessionAsync(userId, cancellationToken);
        _logger.LogInformation("AI 会话已创建 user={UserId} session={SessionId}（审计）", userId, sessionId);
        return new AiSessionCreatedResponse(sessionId, Disclaimer);
    }

    /// <summary>发送消息。命中危机抛 AiCrisisException（控制器转 7101）。</summary>
    public async Task<AiReplyResponse> SendAsync(
        long userId, long sessionId, string content, CancellationToken cancellationToken)
    {
        // ① 限流
        if (!TryAcquireRateSlot(userId))
        {
            _logger.LogWarning("AI 限流触发 user={UserId} session={SessionId}（审计）", userId, sessionId);
            throw new BizException(ErrorCodes.AiRateLimited, "AI 会话超过频率限制，请稍后再试");
        }

        if (!await _ai.SessionBelongsToAsync(sessionId, userId, cancellationToken))
        {
            throw new BizException(ErrorCodes.NotFound, "会话不存在");
        }

        var text = content.Trim();

        // ② 危机前置拦截（在调用模型之前）
        var matched = _detection.Match(text);
        var userMessageId = await _ai.InsertMessageAsync(
            sessionId, "user", _cipher.Encrypt(text), matched.Count > 0, cancellationToken);
        if (matched.Count > 0)
        {
            var safeReply = SafeReply();
            var replyId = await _ai.InsertMessageAsync(
                sessionId, "assistant", _cipher.Encrypt(safeReply), isCrisis: false, cancellationToken);
            _logger.LogWarning(
                "AI 危机拦截 user={UserId} session={SessionId} userMsg={MessageId} 未调用模型（审计）",
                userId, sessionId, userMessageId);
            throw new AiCrisisException(new AiCrisisResponse(sessionId, replyId, safeReply, CrisisHelpUrl));
        }

        // ③ 未命中 → 调用模型（或 mock）
        var reply = await CallModelAsync(text, cancellationToken);
        var assistantMessageId = await _ai.InsertMessageAsync(
            sessionId, "assistant", _cipher.Encrypt(reply), isCrisis: false, cancellationToken);

        _logger.LogInformation(
            "AI 调用完成 user={UserId} session={SessionId} userMsg={UserMessageId} assistantMsg={AssistantMessageId}（审计）",
            userId, sessionId, userMessageId, assistantMessageId);
        return new AiReplyResponse(sessionId, assistantMessageId, reply);
    }

    public async Task<AiMessagesResponse?> GetMessagesAsync(
        long userId, long sessionId, CancellationToken cancellationToken)
    {
        if (!await _ai.SessionBelongsToAsync(sessionId, userId, cancellationToken))
        {
            return null;
        }
        var rows = await _ai.ListMessagesAsync(sessionId, cancellationToken);
        var messages = rows.Select(r => new AiMessageItem(
            r.Id, r.Role, _cipher.Decrypt(r.ContentCipher), r.CreatedAt)).ToList();
        return new AiMessagesResponse(sessionId, messages, Disclaimer);
    }

    public async Task<IReadOnlyList<AiSessionItem>> ListSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        var rows = await _ai.ListSessionsAsync(userId, cancellationToken);
        return rows.Select(r => new AiSessionItem(r.Item1, r.Item2, r.Item3, r.Item4)).ToList();
    }

    private string SafeReply()
    {
        var configured = _configuration["CRISIS_SAFE_REPLY"]?.Trim();
        return string.IsNullOrWhiteSpace(configured) || configured == "CHANGE_ME_BEFORE_LAUNCH"
            ? FallbackSafeReply
            : configured;
    }

    private bool TryAcquireRateSlot(long userId)
    {
        var now = DateTime.UtcNow;
        var bucket = RateBuckets.AddOrUpdate(
            userId,
            _ => (1, now),
            (_, existing) => now - existing.WindowStart >= RateLimitWindow
                ? (1, now)
                : (existing.Count + 1, existing.WindowStart));
        return bucket.Count <= RateLimitCount;
    }

    /// <summary>模型调用：配置了 AI_API_BASE 走 OpenAI 兼容 chat/completions；否则 Development 返回 mock。</summary>
    private async Task<string> CallModelAsync(string userText, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["AI_API_BASE"]?.Trim();
        var apiKey = _configuration["AI_API_KEY"]?.Trim();
        var model = _configuration["AI_MODEL"]?.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            if (_environment.IsDevelopment())
            {
                // mock 回复：不诊断、不建议用药
                return "谢谢你愿意说出来。我会一直在这里陪你聊。需要提醒你：我不是医生或心理治疗师，不能诊断，也不建议用药。" +
                       "如果你愿意，可以多说说最近发生了什么；也可以去看看科普文章，或做一份自评量表了解自己的状态。";
            }
            throw new BizException(ErrorCodes.InternalError, "AI 服务未配置");
        }

        var payload = new
        {
            model = model ?? "gpt-4o-mini",
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt() },
                new { role = "user", content = userText },
            },
        };
        var client = _httpClientFactory.CreateClient("ai");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content ?? "（模型返回为空）";
    }

    /// <summary>系统提示词：非医生/非治疗师声明 + 危机引导（硬性规则 6）。</summary>
    public static string SystemPrompt() => """
        你是"心灯"的 AI 陪伴助手，为经历情绪困扰的用户提供温暖的陪伴式对话。
        你必须始终表明：你不是医生或心理治疗师，不能诊断，也不建议用药（硬性规则 6）。
        你不评判、不说教；多倾听、多共情；用户提到自伤自杀念头时，引导其使用站内求助资源并联系专业帮助。
        不收集与对话无关的个人信息（数据最小化）。
        """;
}

/// <summary>AI 危机拒答：控制器转为 code=7101 的统一返回体（HTTP 200）。</summary>
public sealed class AiCrisisException : Exception
{
    public AiCrisisResponse Response { get; }
    public AiCrisisException(AiCrisisResponse response) : base("AI 危机拒答") => Response = response;
}
