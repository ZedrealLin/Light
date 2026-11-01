using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record DiaryRow(
    long Id,
    long UserId,
    DateOnly EntryDate,
    int MoodScore,
    byte[] ContentCipher,
    string? TagsJson,
    bool IsCrisis,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DeletedAt);

/// <summary>
/// 情绪日记仓储。正文一律密文（content_cipher），仓储层不做加解密；
/// 软删除不释放 (user_id, entry_date) 唯一键——同日期重建走"恢复/覆盖"（er-design §4.5）。
/// </summary>
public sealed class DiaryRepository
{
    private readonly IDbConnectionFactory _factory;

    public DiaryRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    private const string RowColumns = """
        id, user_id, entry_date, mood_score, content_cipher, tags,
        is_crisis, created_at, updated_at, deleted_at
        """;

    /// <summary>取某用户某日期的日记（含已软删除的，供恢复/覆盖判断）。</summary>
    public async Task<DiaryRow?> FindByDateAsync(long userId, DateOnly entryDate, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {RowColumns} FROM diaries WHERE user_id = @userId AND entry_date = @entryDate LIMIT 1";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DiaryRowRaw>(
            new CommandDefinition(sql, new { userId, entryDate }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task<DiaryRow?> FindAsync(long id, long userId, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {RowColumns} FROM diaries WHERE id = @id AND user_id = @userId LIMIT 1";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DiaryRowRaw>(
            new CommandDefinition(sql, new { id, userId }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    /// <summary>新建日记。</summary>
    public async Task<long> InsertAsync(
        long userId, DateOnly entryDate, int moodScore, byte[] contentCipher,
        string? tagsJson, bool isCrisis, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO diaries (user_id, entry_date, mood_score, content_cipher, tags, is_crisis)
            VALUES (@userId, @entryDate, @moodScore, @contentCipher, @tagsJson, @isCrisis);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            userId, entryDate, moodScore, contentCipher, tagsJson, isCrisis,
        }, cancellationToken: cancellationToken));
    }

    /// <summary>更新日记（含恢复软删除：deleted_at 置空）。</summary>
    public async Task UpdateAsync(
        long id, int moodScore, byte[] contentCipher, string? tagsJson, bool isCrisis,
        bool restoreDeleted, CancellationToken cancellationToken = default)
    {
        var sql = restoreDeleted
            ? """
              UPDATE diaries
              SET mood_score = @moodScore, content_cipher = @contentCipher, tags = @tagsJson,
                  is_crisis = @isCrisis, deleted_at = NULL
              WHERE id = @id
              """
            : """
              UPDATE diaries
              SET mood_score = @moodScore, content_cipher = @contentCipher, tags = @tagsJson,
                  is_crisis = @isCrisis
              WHERE id = @id
              """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            id, moodScore, contentCipher, tagsJson, isCrisis,
        }, cancellationToken: cancellationToken));
    }

    public async Task SoftDeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE diaries SET deleted_at = UTC_TIMESTAMP(3) WHERE id = @id AND deleted_at IS NULL";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken));
    }

    /// <summary>列表（不含正文明文与密文）。</summary>
    public async Task<(IReadOnlyList<DiaryRow> Items, int Total)> ListAsync(
        long userId, DateOnly? from, DateOnly? to, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var where = "user_id = @userId AND deleted_at IS NULL";
        if (from is not null)
        {
            where += " AND entry_date >= @from";
        }
        if (to is not null)
        {
            where += " AND entry_date <= @to";
        }

        var sql = $"""
            SELECT id, entry_date, mood_score, tags, is_crisis, created_at
            FROM diaries
            WHERE {where}
            ORDER BY entry_date DESC, id DESC
            LIMIT @limit OFFSET @offset
            """;
        var countSql = $"SELECT COUNT(*) FROM diaries WHERE {where}";

        await using var connection = await _factory.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<ListRow>(new CommandDefinition(sql, new
        {
            userId, from, to, limit = pageSize, offset = (page - 1) * pageSize,
        }, cancellationToken: cancellationToken))).ToList();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { userId, from, to }, cancellationToken: cancellationToken));
        return (items.Select(r => new DiaryRow(r.id, userId, r.entry_date, r.mood_score,
                Array.Empty<byte>(), r.tags, r.is_crisis, r.created_at, r.created_at, null)).ToList(),
            total);
    }

    /// <summary>趋势数据（近 N 天，轻量列）。</summary>
    public async Task<IReadOnlyList<(DateOnly Date, int MoodScore)>> TrendAsync(
        long userId, int days, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT entry_date, mood_score
            FROM diaries
            WHERE user_id = @userId AND deleted_at IS NULL
              AND entry_date >= @startDate
            ORDER BY entry_date
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<(DateOnly, int)>(
            new CommandDefinition(sql, new
            {
                userId,
                startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days),
            }, cancellationToken: cancellationToken));
        return rows.Select(r => (r.Item1, r.Item2)).ToList();
    }

    // ---------- 提醒设置 ----------

    public async Task<(bool Enabled, string RemindTime, string DaysOfWeek)?> GetReminderAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT is_enabled, remind_time, days_of_week FROM diary_reminders WHERE user_id = @userId LIMIT 1";
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ReminderRow>(
            new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
        return row is null ? null : (row.is_enabled == 1, row.remind_time.ToString(@"hh\:mm\:ss")[..5], row.days_of_week);
    }

    public async Task UpsertReminderAsync(
        long userId, bool enabled, string remindTime, string daysOfWeek, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO diary_reminders (user_id, is_enabled, remind_time, days_of_week)
            VALUES (@userId, @enabled, @remindTime, @daysOfWeek)
            ON DUPLICATE KEY UPDATE is_enabled = @enabled, remind_time = @remindTime, days_of_week = @daysOfWeek
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            userId, enabled = enabled ? 1 : 0, remindTime, daysOfWeek,
        }, cancellationToken: cancellationToken));
    }

    private sealed class DiaryRowRaw
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public DateOnly entry_date { get; set; }
        public int mood_score { get; set; }
        public byte[] content_cipher { get; set; } = Array.Empty<byte>();
        public string? tags { get; set; }
        public bool is_crisis { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public DateTime? deleted_at { get; set; }

        public DiaryRow ToRecord() => new(id, user_id, entry_date, mood_score, content_cipher, tags, is_crisis, created_at, updated_at, deleted_at);
    }

    private sealed class ListRow
    {
        public long id { get; set; }
        public DateOnly entry_date { get; set; }
        public int mood_score { get; set; }
        public string? tags { get; set; }
        public bool is_crisis { get; set; }
        public DateTime created_at { get; set; }
    }

    private sealed class ReminderRow
    {
        public sbyte is_enabled { get; set; }
        public TimeSpan remind_time { get; set; }
        public string days_of_week { get; set; } = "1,2,3,4,5,6,7";
    }
}
