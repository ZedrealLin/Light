using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Light.Api.Infrastructure.Db;

/// <summary>MySQL 连接工厂抽象（便于测试替身）。</summary>
public interface IDbConnectionFactory
{
    /// <summary>连接串是否已配置（未配置时健康检查如实上报，不抛异常）。</summary>
    bool IsConfigured { get; }

    /// <summary>打开一个新连接。未配置时抛出带明确指引的异常。</summary>
    Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// MySQL 连接工厂：连接串只来自配置层（本地开发用环境变量 ConnectionStrings__Default 注入）。
/// 硬性规则 9：任何凭据不得硬编码、不得进入代码仓库。
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string? _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "数据库未配置：请通过环境变量 ConnectionStrings__Default 提供连接串（硬性规则 9：凭据不硬编码）");
        }

        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
