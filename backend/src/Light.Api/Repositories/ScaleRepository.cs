using System.Text.Json;
using Dapper;
using Light.Api.Infrastructure.Db;
using Light.Api.Models.Scale;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record ScaleSummary(
    string Code,
    string Name,
    string? Description,
    int QuestionCount,
    int EstimatedMinutes,
    string? SourceName,
    bool SourceVerified);

/// <summary>量表定义仓储：只读配置，写操作归后台（阶段 1 后台接口）。</summary>
public sealed class ScaleRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<ScaleRepository> _logger;

    public ScaleRepository(IDbConnectionFactory factory, ILogger<ScaleRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>启用中的量表概要列表（不含题目明细）。</summary>
    public async Task<IReadOnlyList<ScaleSummary>> GetActiveSummariesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT code, name, config_json
            FROM scales
            WHERE is_active = 1
            ORDER BY id
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<ScaleRow>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();

        var summaries = new List<ScaleSummary>(rows.Count);
        foreach (var row in rows)
        {
            ScaleConfigDocument? config;
            try
            {
                config = JsonSerializer.Deserialize<ScaleConfigDocument>(row.config_json);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "量表 {Code} 的 config_json 反序列化失败，已跳过该量表", row.code);
                continue;
            }
            if (config is null || config.Questions.Count == 0)
            {
                _logger.LogWarning("量表 {Code} 的配置为空或无题目，已跳过", row.code);
                continue;
            }
            var minutes = Math.Max(1, (int)Math.Ceiling(config.Questions.Count / 4.0));
            summaries.Add(new ScaleSummary(
                row.code, row.name, config.Description, config.Questions.Count, minutes,
                config.Source?.Name, config.Source?.Verified ?? false));
        }
        return summaries;
    }

    /// <summary>按代码取启用中的最高版本配置；不存在返回 null（对外 3001）。</summary>
    public async Task<ScaleConfigDocument?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT config_json
            FROM scales
            WHERE code = @code AND is_active = 1
            ORDER BY version DESC
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var json = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(sql, new { code }, cancellationToken: cancellationToken));
        if (json is null)
        {
            return null;
        }
        return JsonSerializer.Deserialize<ScaleConfigDocument>(json);
    }

    public sealed record ActiveScaleRow(long Id, string Code, int Version, ScaleConfigDocument Config);

    /// <summary>按代码取启用中的最高版本（含 id / version，供提交落库）；不存在返回 null。</summary>
    public async Task<ActiveScaleRow?> GetActiveRowAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, code, version, config_json
            FROM scales
            WHERE code = @code AND is_active = 1
            ORDER BY version DESC
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ActiveRawRow>(
            new CommandDefinition(sql, new { code }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }
        var config = JsonSerializer.Deserialize<ScaleConfigDocument>(row.config_json)
            ?? throw new InvalidOperationException($"量表 {row.code} 的 config_json 反序列化失败");
        return new ActiveScaleRow(row.id, row.code, row.version, config);
    }

    private sealed class ActiveRawRow
    {
        public long id { get; set; }
        public string code { get; set; } = string.Empty;
        public int version { get; set; }
        public string config_json { get; set; } = string.Empty;
    }

    private sealed class ScaleRow
    {
        public string code { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string config_json { get; set; } = string.Empty;
    }
}
