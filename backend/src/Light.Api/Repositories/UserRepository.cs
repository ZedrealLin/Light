using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record UserRow(
    long Id,
    string PublicId,
    string? Phone,
    string? Email,
    string? PasswordHash,
    string? Nickname,
    string Status,
    DateTime? PrivacyConsentAt,
    DateTime? DeletedAt);

/// <summary>用户与刷新令牌仓储。注销（匿名化）在本仓储显式实现，保证 PII 清理可审计。</summary>
public sealed class UserRepository
{
    private readonly IDbConnectionFactory _factory;

    public UserRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>按手机号或邮箱查用户（登录/注册判重）。</summary>
    public async Task<UserRow?> FindByAccountAsync(string account, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, public_id, phone, email, password_hash, nickname, status,
                   privacy_consent_at, deleted_at
            FROM users
            WHERE (phone = @account OR email = @account) AND deleted_at IS NULL
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRowRaw>(
            new CommandDefinition(sql, new { account }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task<UserRow?> FindByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, public_id, phone, email, password_hash, nickname, status,
                   privacy_consent_at, deleted_at
            FROM users
            WHERE id = @id AND deleted_at IS NULL
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRowRaw>(
            new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    /// <summary>创建用户（验证码注册的账户无密码）。返回新用户 id。</summary>
    public async Task<long> InsertAsync(
        string? phone, string? email, string? passwordHash, string? nickname,
        DateTime? privacyConsentAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO users (public_id, phone, email, password_hash, nickname, privacy_consent_at, privacy_consent_version)
            VALUES (@publicId, @phone, @email, @passwordHash, @nickname, @privacyConsentAt, @privacyConsentVersion);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            publicId = Guid.CreateVersion7().ToString(),
            phone,
            email,
            passwordHash,
            nickname,
            privacyConsentAt,
            privacyConsentVersion = privacyConsentAt is null ? null : "v1",
        }, cancellationToken: cancellationToken));
    }

    public async Task RecordPrivacyConsentAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE users
            SET privacy_consent_at = UTC_TIMESTAMP(3), privacy_consent_version = 'v1'
            WHERE id = @userId
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateProfileAsync(long userId, string? nickname, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE users SET nickname = @nickname WHERE id = @userId";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { userId, nickname }, cancellationToken: cancellationToken));
    }

    /// <summary>注销：匿名化并保留密文数据（L5 已确认）——清空 PII、标记状态与时间。</summary>
    public async Task DeactivateAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE users
            SET phone = NULL, email = NULL, password_hash = NULL,
                nickname = '（已注销用户）', avatar_url = NULL,
                status = 'anonymized', deleted_at = UTC_TIMESTAMP(3)
            WHERE id = @userId
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
    }

    // ---------- 刷新令牌 ----------

    public async Task InsertRefreshTokenAsync(long userId, string tokenHash, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO refresh_tokens (user_id, token_hash, expires_at)
            VALUES (@userId, @tokenHash, @expiresAt)
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { userId, tokenHash, expiresAt }, cancellationToken: cancellationToken));
    }

    /// <summary>取有效（未撤销、未过期）的刷新令牌及其用户。</summary>
    public async Task<(UserRow User, long TokenId)?> FindValidRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT u.id, u.public_id, u.phone, u.email, u.password_hash, u.nickname, u.status,
                   u.privacy_consent_at, u.deleted_at,
                   r.id AS token_id
            FROM refresh_tokens r
            JOIN users u ON u.id = r.user_id
            WHERE r.token_hash = @tokenHash
              AND r.revoked_at IS NULL
              AND r.expires_at > UTC_TIMESTAMP(3)
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<RefreshJoinRow>(
            new CommandDefinition(sql, new { tokenHash }, cancellationToken: cancellationToken));
        return row is null ? null : (row.ToUser(), row.token_id);
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE refresh_tokens SET revoked_at = UTC_TIMESTAMP(3) WHERE token_hash = @tokenHash AND revoked_at IS NULL";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { tokenHash }, cancellationToken: cancellationToken));
    }

    /// <summary>撤销某用户全部刷新令牌（注销时调用）。</summary>
    public async Task RevokeAllRefreshTokensAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE refresh_tokens SET revoked_at = UTC_TIMESTAMP(3)
            WHERE user_id = @userId AND revoked_at IS NULL
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// 彻底删除用户及其全部关联数据（硬性规则 4"删除我的数据"）。
    /// 按 FK 依赖顺序在事务内显式删除（多为 ON DELETE RESTRICT，必须显式处理，保证可审计）。
    /// </summary>
    public async Task DeleteUserCompletelyAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var steps = new[]
            {
                "DELETE FROM crisis_alerts WHERE user_id = @userId",
                "DELETE FROM notifications WHERE user_id = @userId",
                "DELETE FROM diary_reminders WHERE user_id = @userId",
                "DELETE FROM refresh_tokens WHERE user_id = @userId",
                "DELETE FROM bookings WHERE user_id = @userId",
                "DELETE FROM diaries WHERE user_id = @userId",
                "DELETE m FROM ai_messages m JOIN ai_sessions s ON s.id = m.session_id WHERE s.user_id = @userId",
                "DELETE FROM ai_sessions WHERE user_id = @userId",
                "DELETE FROM scale_submissions WHERE user_id = @userId",
                "DELETE FROM users WHERE id = @userId",
            };
            foreach (var sql in steps)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    sql, new { userId }, transaction, cancellationToken: cancellationToken));
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed class UserRowRaw
    {
        public long id { get; set; }

        // MySqlConnector 默认把 CHAR(36) 映射为 Guid
        public Guid public_id { get; set; }
        public string? phone { get; set; }
        public string? email { get; set; }
        public string? password_hash { get; set; }
        public string? nickname { get; set; }
        public string status { get; set; } = "active";
        public DateTime? privacy_consent_at { get; set; }
        public DateTime? deleted_at { get; set; }

        public UserRow ToRecord() => new(id, public_id.ToString(), phone, email, password_hash, nickname, status, privacy_consent_at, deleted_at);
    }

    private sealed class RefreshJoinRow
    {
        public long id { get; set; }
        public Guid public_id { get; set; }
        public string? phone { get; set; }
        public string? email { get; set; }
        public string? password_hash { get; set; }
        public string? nickname { get; set; }
        public string status { get; set; } = "active";
        public DateTime? privacy_consent_at { get; set; }
        public DateTime? deleted_at { get; set; }
        public long token_id { get; set; }

        public UserRow ToUser() => new(id, public_id.ToString(), phone, email, password_hash, nickname, status, privacy_consent_at, deleted_at);
    }
}
