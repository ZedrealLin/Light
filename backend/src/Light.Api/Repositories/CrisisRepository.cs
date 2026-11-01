using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record CrisisAlertInsert(
    string Source,            // scale / diary / community / ai_chat
    long? UserId,
    long? ScaleSubmissionId,
    long? DiaryId,
    long? AiSessionId,
    string? ExternalRef,
    string DetailJson);

/// <summary>危机干预数据访问：关键词读取 + 预警工单写入与去重查询。</summary>
public sealed class CrisisRepository
{
    private readonly IDbConnectionFactory _factory;

    public CrisisRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<string>> GetActiveKeywordsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT keyword FROM crisis_keywords WHERE is_active = 1 ORDER BY id";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<string>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    /// <summary>
    /// 工单去重（crisis-flow §3）：同一 (source, 关联对象) 在 windowMinutes 内已有工单则不再落单。
    /// 只按传入的非空关联字段匹配。
    /// </summary>
    public Task<bool> HasRecentAlertAsync(
        CrisisAlertInsert alert, int windowMinutes, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string> { "source = @Source" };
        if (alert.ScaleSubmissionId is not null)
        {
            conditions.Add("scale_submission_id = @ScaleSubmissionId");
        }
        if (alert.DiaryId is not null)
        {
            conditions.Add("diary_id = @DiaryId");
        }
        if (alert.AiSessionId is not null)
        {
            conditions.Add("ai_session_id = @AiSessionId");
        }
        if (alert.ExternalRef is not null)
        {
            conditions.Add("external_ref = @ExternalRef");
        }
        if (conditions.Count == 1)
        {
            // 没有可关联的对象时，退化为按用户 + 时间窗去重
            conditions.Add("user_id = @UserId");
        }
        conditions.Add($"created_at >= UTC_TIMESTAMP(3) - INTERVAL {windowMinutes} MINUTE");

        var sql = $"SELECT COUNT(*) FROM crisis_alerts WHERE {string.Join(" AND ", conditions)}";
        return QueryCountAsync(sql, alert, cancellationToken);
    }

    /// <summary>写入预警工单，返回工单 id。</summary>
    public async Task<long> InsertAlertAsync(CrisisAlertInsert alert, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO crisis_alerts
              (source, user_id, scale_submission_id, diary_id, ai_session_id, external_ref, detail_json)
            VALUES
              (@Source, @UserId, @ScaleSubmissionId, @DiaryId, @AiSessionId, @ExternalRef, @DetailJson);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, alert, cancellationToken: cancellationToken));
    }

    private async Task<bool> QueryCountAsync(string sql, CrisisAlertInsert alert, CancellationToken cancellationToken)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, alert, cancellationToken: cancellationToken));
        return count > 0;
    }
}
