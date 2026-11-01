using System.Text;
using Light.Api.Infrastructure.Db;
using MySqlConnector;

namespace Light.Api.Infrastructure.Migrations;

/// <summary>
/// 自写迁移工具：启动时顺序执行输出目录 db/migrations 下的 *.sql，并记录到 schema_migrations。
/// 规则（CONVENTIONS 4.2 / AGENTS）：
///   1. 脚本按文件名升序执行（NNNN_描述.sql）；
///   2. 脚本必须幂等（CREATE TABLE IF NOT EXISTS 等），可重复执行；
///   3. MySQL 的 DDL 会隐式提交，因此逐文件执行、执行成功后逐文件记录，不做跨文件事务。
/// </summary>
public sealed class DbMigrator
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<DbMigrator> _logger;

    public DbMigrator(IDbConnectionFactory factory, ILogger<DbMigrator> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (!_factory.IsConfigured)
        {
            _logger.LogWarning("数据库未配置（ConnectionStrings__Default），跳过迁移。");
            return;
        }

        var directory = Path.Combine(AppContext.BaseDirectory, "db", "migrations");
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("迁移目录不存在：{Directory}", directory);
            return;
        }

        var files = Directory.GetFiles(directory, "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            _logger.LogWarning("迁移目录为空：{Directory}", directory);
            return;
        }

        await using var connection = await _factory.OpenAsync(cancellationToken);
        await EnsureMigrationsTableAsync(connection, cancellationToken);

        var applied = 0;
        var skipped = 0;
        foreach (var file in files)
        {
            var version = Path.GetFileName(file);
            if (await IsAppliedAsync(connection, version, cancellationToken))
            {
                skipped++;
                continue;
            }

            _logger.LogInformation("应用迁移：{Version}", version);
            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            foreach (var statement in SplitStatements(sql))
            {
                await using var command = new MySqlCommand(statement, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await RecordAppliedAsync(connection, version, cancellationToken);
            applied++;
        }

        _logger.LogInformation("迁移完成：新应用 {Applied} 个，跳过 {Skipped} 个（共 {Total} 个）", applied, skipped, files.Count);
    }

    private static async Task EnsureMigrationsTableAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
              version    VARCHAR(128) NOT NULL COMMENT '迁移文件名，如 0001_init.sql',
              applied_at DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
              PRIMARY KEY (version)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='迁移记录（迁移工具维护）';
            """;
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(MySqlConnection connection, string version, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM schema_migrations WHERE version = @version",
            connection);
        command.Parameters.AddWithValue("@version", version);
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    private static async Task RecordAppliedAsync(MySqlConnection connection, string version, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "INSERT INTO schema_migrations (version) VALUES (@version)",
            connection);
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 把脚本按"顶层分号"切分为单条语句：单引号/双引号/反引号内的分号与 -- 行注释中的分号不参与切分。
    /// 放在此处是为了不依赖驱动的多语句执行语义，行为完全可测。
    /// </summary>
    internal static IEnumerable<string> SplitStatements(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var inSingle = false;
        var inDouble = false;
        var inBacktick = false;
        var inLineComment = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];

            if (inLineComment)
            {
                builder.Append(c);
                if (c == '\n')
                {
                    inLineComment = false;
                }
                continue;
            }
            if (!inSingle && !inDouble && !inBacktick && c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                inLineComment = true;
                builder.Append(c);
                continue;
            }
            if (inSingle)
            {
                builder.Append(c);
                if (c == '\'')
                {
                    // MySQL 中 '' 为转义单引号
                    if (i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        builder.Append('\'');
                        i++;
                    }
                    else
                    {
                        inSingle = false;
                    }
                }
                continue;
            }
            if (inDouble)
            {
                builder.Append(c);
                if (c == '"')
                {
                    inDouble = false;
                }
                continue;
            }
            if (inBacktick)
            {
                builder.Append(c);
                if (c == '`')
                {
                    inBacktick = false;
                }
                continue;
            }

            switch (c)
            {
                case '\'':
                    inSingle = true;
                    builder.Append(c);
                    break;
                case '"':
                    inDouble = true;
                    builder.Append(c);
                    break;
                case '`':
                    inBacktick = true;
                    builder.Append(c);
                    break;
                case ';':
                {
                    var statement = builder.ToString().Trim();
                    builder.Clear();
                    if (statement.Length > 0)
                    {
                        yield return statement;
                    }
                    break;
                }
                default:
                    builder.Append(c);
                    break;
            }
        }

        var tail = builder.ToString().Trim();
        if (tail.Length > 0)
        {
            yield return tail;
        }
    }
}
