using System.Text.Json;
using Dapper;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Repositories;

public sealed record ScaleSubmissionRow(
    long Id,
    string PublicId,
    long? UserId,
    string ScaleCode,
    int ScaleVersion,
    byte[] AnswersCipher,
    int TotalScore,
    string LevelCode,
    string LevelLabel,
    bool IsCrisis,
    DateTime CreatedAt);

/// <summary>量表作答记录仓储。answers 一律以密文落库（硬性规则 3），仓储层不做加解密。</summary>
public sealed class ScaleSubmissionRepository
{
    private readonly IDbConnectionFactory _factory;

    public ScaleSubmissionRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>写入作答记录，返回含 public_id 的行。answersCipher 由服务层用 IAesCipher 加密后传入。</summary>
    public async Task<ScaleSubmissionRow> InsertAsync(
        long? userId,
        long scaleId,
        string scaleCode,
        int scaleVersion,
        byte[] answersCipher,
        int totalScore,
        string levelCode,
        string levelLabel,
        bool isCrisis,
        CancellationToken cancellationToken = default)
    {
        var publicId = Guid.CreateVersion7().ToString();

        const string sql = """
            INSERT INTO scale_submissions
              (public_id, user_id, scale_id, scale_code, scale_version,
               answers_cipher, total_score, level_code, level_label, is_crisis)
            VALUES
              (@publicId, @userId, @scaleId, @scaleCode, @scaleVersion,
               @answersCipher, @totalScore, @levelCode, @levelLabel, @isCrisis);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = await _factory.OpenAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            publicId,
            userId,
            scaleId,
            scaleCode,
            scaleVersion,
            answersCipher,
            totalScore,
            levelCode,
            levelLabel,
            isCrisis,
        }, cancellationToken: cancellationToken));

        return new ScaleSubmissionRow(
            id, publicId, userId, scaleCode, scaleVersion,
            answersCipher, totalScore, levelCode, levelLabel, isCrisis,
            DateTime.UtcNow);
    }

    /// <summary>按 public_id 取作答记录；不存在返回 null（对外 1001）。</summary>
    public async Task<ScaleSubmissionRow?> GetByPublicIdAsync(string publicId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, public_id, user_id, scale_code, scale_version,
                   answers_cipher, total_score, level_code, level_label, is_crisis, created_at
            FROM scale_submissions
            WHERE public_id = @publicId
            LIMIT 1
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<SubmissionRow>(new CommandDefinition(
            sql, new { publicId }, cancellationToken: cancellationToken));
        return row is null ? null : row.ToRecord();
    }

    /// <summary>导出用：该用户全部作答记录（总分/分级为明文最小字段；答案密文不导出）。</summary>
    public async Task<IReadOnlyList<ScaleSubmissionRow>> ListForExportAsync(long userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, public_id, user_id, scale_code, scale_version,
                   answers_cipher, total_score, level_code, level_label, is_crisis, created_at
            FROM scale_submissions
            WHERE user_id = @userId
            ORDER BY id
            """;
        await using var connection = await _factory.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<SubmissionRow>(
            new CommandDefinition(sql, new { userId }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToRecord()).ToList();
    }

    private sealed class SubmissionRow
    {
        public long id { get; set; }

        // MySqlConnector 默认把 CHAR(36) 映射为 Guid，属性必须用 Guid 接
        public Guid public_id { get; set; }
        public long? user_id { get; set; }
        public string scale_code { get; set; } = string.Empty;
        public int scale_version { get; set; }
        public byte[] answers_cipher { get; set; } = Array.Empty<byte>();
        public int total_score { get; set; }
        public string level_code { get; set; } = string.Empty;
        public string level_label { get; set; } = string.Empty;
        public bool is_crisis { get; set; }
        public DateTime created_at { get; set; }

        public ScaleSubmissionRow ToRecord() =>
            new(id, public_id.ToString(), user_id, scale_code, scale_version,
                answers_cipher, total_score, level_code, level_label, is_crisis, created_at);
    }
}
