using System.Text.Json;
using Light.Api.Repositories;

namespace Light.Api.Services;

/// <summary>
/// 危机预警统一动作（crisis-flow §2/§3）：
/// 命中 → 10 分钟窗口去重 → 写入 crisis_alerts（status=open，detail_json 最小化快照）→ 记 Warning 日志供后台关注。
/// 前端求助页展示与 AI 安全话术由各调用方在响应中携带（本服务只负责落单与通知后台的日志）。
/// </summary>
public sealed class CrisisAlertService
{
    public const int DedupWindowMinutes = 10;

    private readonly CrisisRepository _repository;
    private readonly ILogger<CrisisAlertService> _logger;

    public CrisisAlertService(CrisisRepository repository, ILogger<CrisisAlertService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>量表来源建单：detail 快照只含量表代码/总分/触发规则，不含答案明细。</summary>
    public Task<long?> TryCreateScaleAlertAsync(
        long scaleSubmissionId,
        string submissionPublicId,
        string scaleCode,
        int totalScore,
        IReadOnlyList<string> triggeredRules,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        var detail = JsonSerializer.Serialize(new
        {
            scaleCode,
            submissionPublicId,
            totalScore,
            triggeredRules,
        });

        return TryCreateAsync(new CrisisAlertInsert(
            Source: "scale",
            UserId: userId,
            ScaleSubmissionId: scaleSubmissionId,
            DiaryId: null,
            AiSessionId: null,
            ExternalRef: null,
            DetailJson: detail), cancellationToken);
    }

    /// <summary>关键词来源（日记/社区/AI）建单：detail 快照只含命中关键词，不含正文全文。</summary>
    public Task<long?> TryCreateKeywordAlertAsync(
        string source,
        long? userId,
        long? diaryId,
        long? aiSessionId,
        string? externalRef,
        IReadOnlyList<string> matchedKeywords,
        CancellationToken cancellationToken = default)
    {
        var detail = JsonSerializer.Serialize(new
        {
            matchedKeywords,
        });

        return TryCreateAsync(new CrisisAlertInsert(
            Source: source,
            UserId: userId,
            ScaleSubmissionId: null,
            DiaryId: diaryId,
            AiSessionId: aiSessionId,
            ExternalRef: externalRef,
            DetailJson: detail), cancellationToken);
    }

    private async Task<long?> TryCreateAsync(CrisisAlertInsert alert, CancellationToken cancellationToken)
    {
        try
        {
            if (await _repository.HasRecentAlertAsync(alert, DedupWindowMinutes, cancellationToken))
            {
                _logger.LogInformation(
                    "危机预警去重命中：source={Source} 在 {Minutes} 分钟内已有工单，跳过落单（求助展示仍执行）",
                    alert.Source, DedupWindowMinutes);
                return null;
            }

            var alertId = await _repository.InsertAlertAsync(alert, cancellationToken);
            // 后台"通知"：工单出现在待处理列表；此处 Warning 日志便于监控告警接入
            _logger.LogWarning(
                "危机预警工单已建立 alertId={AlertId} source={Source} userId={UserId}",
                alertId, alert.Source, alert.UserId);
            return alertId;
        }
        catch (Exception ex)
        {
            // 建单失败绝不能阻断用户拿到求助入口；记错误日志人工兜底
            _logger.LogError(ex, "危机预警工单写入失败 source={Source}", alert.Source);
            return null;
        }
    }
}
