using Light.Api.Services;

namespace Light.Api.Infrastructure.Jobs;

/// <summary>
/// 后台任务（BackgroundService）：每 30 秒刷新危机关键词缓存（crisis-flow §6）。
/// 后台在管理端增删关键词后，最迟 30 秒生效。
/// </summary>
public sealed class CrisisKeywordRefreshJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly CrisisDetectionService _detection;
    private readonly ILogger<CrisisKeywordRefreshJob> _logger;

    public CrisisKeywordRefreshJob(CrisisDetectionService detection, ILogger<CrisisKeywordRefreshJob> logger)
    {
        _detection = detection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _detection.ReloadAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "危机关键词定时刷新异常（将在下个周期重试）");
            }
        }
    }
}
