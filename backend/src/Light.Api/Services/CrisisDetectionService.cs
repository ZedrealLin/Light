using Light.Api.Infrastructure.Db;
using Light.Api.Repositories;

namespace Light.Api.Services;

/// <summary>
/// 危机关键词检测（crisis-flow §1/§6）：
/// 启动时与后台每 30 秒从 crisis_keywords 加载启用词集到内存；匹配为大小写不敏感的包含匹配。
/// 扩展更复杂的匹配（分词/正则）只改本类单点实现。
/// </summary>
public sealed class CrisisDetectionService
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<CrisisDetectionService> _logger;
    private volatile string[] _keywords = Array.Empty<string>();

    public CrisisDetectionService(IDbConnectionFactory factory, ILogger<CrisisDetectionService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>当前启用关键词数量（健康检查/诊断用）。</summary>
    public int KeywordCount => _keywords.Length;

    /// <summary>从数据库重新加载启用关键词。</summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (!_factory.IsConfigured)
        {
            return;
        }
        try
        {
            var keywords = (await new CrisisRepository(_factory)
                    .GetActiveKeywordsAsync(cancellationToken))
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _keywords = keywords;
            _logger.LogInformation("危机关键词已加载：{Count} 个", keywords.Length);
        }
        catch (Exception ex)
        {
            // 词表刷新失败不应影响线上匹配（沿用旧词集），只记日志
            _logger.LogWarning(ex, "危机关键词刷新失败，沿用现有词集（{Count} 个）", _keywords.Length);
        }
    }

    /// <summary>返回内容命中的关键词列表；未命中返回空集合。content 为 null/空时恒不命中。</summary>
    public IReadOnlyList<string> Match(string? content)
    {
        if (string.IsNullOrEmpty(content) || _keywords.Length == 0)
        {
            return Array.Empty<string>();
        }
        return _keywords.Where(k => content.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
