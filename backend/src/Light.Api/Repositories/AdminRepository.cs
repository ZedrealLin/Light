using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record AdminRow(long Id, string Username, string PasswordHash, string? DisplayName, bool IsActive);

public sealed record AdminAlertRow(
    long Id,
    string Source,
    long? UserId,
    long? ScaleSubmissionId,
    long? DiaryId,
    long? AiSessionId,
    string? ExternalRef,
    string? DetailJson,
    string Status,
    long? HandledBy,
    DateTime? HandledAt,
    string? HandleNote,
    DateTime CreatedAt);

public sealed record AdminUserRow(
    long Id,
    string PublicId,
    string? Phone,
    string? Email,
    string? Nickname,
    string Status,
    DateTime CreatedAt);

/// <summary>后台管理数据访问：管理员、预警工单、用户、量表配置、危机关键词。</summary>
public sealed class AdminRepository
{
    private readonly IDbConnectionFactory _factory;

    public AdminRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<AdminRow?> FindAdminByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, username, password_hash, display_name, is_active
            FROM admins WHERE username = @username LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AdminRaw>(
            new CommandDefinition(sql, new { username }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task UpdateAdminLastLoginAsync(long adminId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE admins SET last_login_at = UTC_TIMESTAMP(3) WHERE id = @adminId";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { adminId }, cancellationToken: cancellationToken));
    }

    /// <summary>预警工单列表（状态筛选 + 分页，含来源用户脱敏昵称）。</summary>
    public async Task<(IReadOnlyList<AdminAlertRow> Items, int Total)> ListAlertsAsync(
        string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var where = "1=1";
        if (!string.IsNullOrEmpty(status))
        {
            where += " AND a.status = @status";
        }
        var sql = $"""
            SELECT a.id, a.source, a.user_id, a.scale_submission_id, a.diary_id, a.ai_session_id,
                   a.external_ref, a.detail_json, a.status, a.handled_by, a.handled_at, a.handle_note, a.created_at
            FROM crisis_alerts a
            WHERE {where}
            ORDER BY a.status = 'open' DESC, a.created_at DESC
            LIMIT @limit OFFSET @offset
            """;
        var countSql = $"SELECT COUNT(*) FROM crisis_alerts a WHERE {where}";

        await using var connection = await _factory.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<AlertRaw>(new CommandDefinition(sql, new
        {
            status, limit = pageSize, offset = (page - 1) * pageSize,
        }, cancellationToken: cancellationToken))).ToList();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { status }, cancellationToken: cancellationToken));
        return (items.Select(r => r.ToRecord()).ToList(), total);
    }

    public async Task<AdminAlertRow?> FindAlertAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, source, user_id, scale_submission_id, diary_id, ai_session_id,
                   external_ref, detail_json, status, handled_by, handled_at, handle_note, created_at
            FROM crisis_alerts WHERE id = @id LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AlertRaw>(
            new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task<bool> HandleAlertAsync(
        long id, long adminId, string action, string? note, CancellationToken cancellationToken = default)
    {
        // 状态机（crisis-flow §4）：open/processing → processing/resolved/dismissed；终态不可改
        const string sql = """
            UPDATE crisis_alerts
            SET status = @action, handled_by = @adminId, handled_at = UTC_TIMESTAMP(3), handle_note = @note
            WHERE id = @id AND status IN ('open','processing')
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            id, adminId, action, note,
        }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<(IReadOnlyList<AdminUserRow> Items, int Total)> ListUsersAsync(
        string? query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var where = "deleted_at IS NULL";
        if (!string.IsNullOrWhiteSpace(query))
        {
            where += " AND (nickname LIKE @like OR phone LIKE @like OR email LIKE @like)";
        }
        var sql = $"""
            SELECT id, public_id, phone, email, nickname, status, created_at
            FROM users
            WHERE {where}
            ORDER BY id DESC
            LIMIT @limit OFFSET @offset
            """;
        var countSql = $"SELECT COUNT(*) FROM users WHERE {where}";

        await using var connection = await _factory.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<UserRaw>(new CommandDefinition(sql, new
        {
            like = $"%{query}%", limit = pageSize, offset = (page - 1) * pageSize,
        }, cancellationToken: cancellationToken))).ToList();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { like = $"%{query}%" }, cancellationToken: cancellationToken));
        return (items.Select(r => r.ToRecord()).ToList(), total);
    }

    public async Task<bool> SetUserStatusAsync(long userId, string status, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE users SET status = @status WHERE id = @userId AND deleted_at IS NULL";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { userId, status }, cancellationToken: cancellationToken)) > 0;
    }

    /// <summary>量表配置更新：新增更高版本行（历史版本不动，见 scales 设计）。</summary>
    public async Task<long> InsertScaleVersionAsync(
        string code, string name, string configJson, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO scales (code, name, version, config_json, is_active)
            SELECT @code, @name, COALESCE(MAX(version), 0) + 1, @configJson, @isActive
            FROM scales WHERE code = @code;
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            code, name, configJson, isActive = isActive ? 1 : 0,
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SetScaleActiveAsync(long scaleId, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE scales SET is_active = @isActive WHERE id = @scaleId";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { scaleId, isActive = isActive ? 1 : 0 }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<IReadOnlyList<string>> ListKeywordsAsync(bool onlyActive, CancellationToken cancellationToken = default)
    {
        var sql = onlyActive
            ? "SELECT keyword FROM crisis_keywords WHERE is_active = 1 ORDER BY id"
            : "SELECT keyword FROM crisis_keywords ORDER BY id";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task AddKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO crisis_keywords (keyword) VALUES (@keyword) ON DUPLICATE KEY UPDATE is_active = 1";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { keyword }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SetKeywordActiveAsync(string keyword, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE crisis_keywords SET is_active = @isActive WHERE keyword = @keyword";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { keyword, isActive = isActive ? 1 : 0 }, cancellationToken: cancellationToken)) > 0;
    }

    private sealed class AdminRaw
    {
        public long id { get; set; }
        public string username { get; set; } = string.Empty;
        public string password_hash { get; set; } = string.Empty;
        public string? display_name { get; set; }
        public sbyte is_active { get; set; }

        public AdminRow ToRecord() => new(id, username, password_hash, display_name, is_active == 1);
    }

    private sealed class AlertRaw
    {
        public long id { get; set; }
        public string source { get; set; } = string.Empty;
        public long? user_id { get; set; }
        public long? scale_submission_id { get; set; }
        public long? diary_id { get; set; }
        public long? ai_session_id { get; set; }
        public string? external_ref { get; set; }
        public string? detail_json { get; set; }
        public string status { get; set; } = string.Empty;
        public long? handled_by { get; set; }
        public DateTime? handled_at { get; set; }
        public string? handle_note { get; set; }
        public DateTime created_at { get; set; }

        public AdminAlertRow ToRecord() => new(id, source, user_id, scale_submission_id, diary_id, ai_session_id,
            external_ref, detail_json, status, handled_by, handled_at, handle_note, created_at);
    }

    private sealed class UserRaw
    {
        public long id { get; set; }
        public Guid public_id { get; set; }
        public string? phone { get; set; }
        public string? email { get; set; }
        public string? nickname { get; set; }
        public string status { get; set; } = string.Empty;
        public DateTime created_at { get; set; }

        public AdminUserRow ToRecord() => new(id, public_id.ToString(), phone, email, nickname, status, created_at);
    }
}
