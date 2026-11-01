using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record AiMessageRow(long Id, string Role, byte[] ContentCipher, bool IsCrisis, DateTime CreatedAt);

/// <summary>AI 陪伴会话与消息仓储。消息内容一律密文落库。</summary>
public sealed class AiRepository
{
    private readonly IDbConnectionFactory _factory;

    public AiRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<long> CreateSessionAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ai_sessions (user_id, title) VALUES (@userId, NULL);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SessionBelongsToAsync(long sessionId, long userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM ai_sessions WHERE id = @sessionId AND user_id = @userId";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { sessionId, userId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<long> InsertMessageAsync(
        long sessionId, string role, byte[] contentCipher, bool isCrisis, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ai_messages (session_id, role, content_cipher, is_crisis)
            VALUES (@sessionId, @role, @contentCipher, @isCrisis);
            UPDATE ai_sessions SET last_message_at = UTC_TIMESTAMP(3) WHERE id = @sessionId;
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            sessionId, role, contentCipher, isCrisis,
        }, cancellationToken: cancellationToken));
    }

    /// <summary>会话消息（按时间正序，最多 200 条）。</summary>
    public async Task<IReadOnlyList<AiMessageRow>> ListMessagesAsync(
        long sessionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, role, content_cipher, is_crisis, created_at
            FROM ai_messages
            WHERE session_id = @sessionId
            ORDER BY id
            LIMIT 200
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MessageRaw>(
            new CommandDefinition(sql, new { sessionId }, cancellationToken: cancellationToken));
        return rows.Select(r => new AiMessageRow(r.id, r.role, r.content_cipher, r.is_crisis, r.created_at)).ToList();
    }

    public async Task<IReadOnlyList<(long Id, string? Title, string Status, DateTime StartedAt)>> ListSessionsAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, title, status, started_at
            FROM ai_sessions
            WHERE user_id = @userId
            ORDER BY id DESC
            LIMIT 50
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<SessionRaw>(
            new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
        return rows.Select(r => (r.id, r.title, r.status, r.started_at)).ToList();
    }

    private sealed class MessageRaw
    {
        public long id { get; set; }
        public string role { get; set; } = string.Empty;
        public byte[] content_cipher { get; set; } = Array.Empty<byte>();
        public bool is_crisis { get; set; }
        public DateTime created_at { get; set; }
    }

    private sealed class SessionRaw
    {
        public long id { get; set; }
        public string? title { get; set; }
        public string status { get; set; } = string.Empty;
        public DateTime started_at { get; set; }
    }
}
