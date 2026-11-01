using Light.Api.Common;
using Light.Api.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace Light.Api.Controllers;

/// <summary>
/// 健康检查（匿名）：部署探针与本地自检用。
/// db 组件在连接串已配置时如实探测（SELECT 1），未配置时报 not_configured；
/// 整体 status 只反映 API 存活（liveness），组件状态供 readiness 参考。
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IDbConnectionFactory _factory;

    public HealthController(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var db = await CheckDbAsync(cancellationToken);
        var data = new
        {
            status = "ok",
            components = new
            {
                api = "ok",
                db,
                redis = "pending",
            },
        };
        return Ok(data.ToOk(HttpContext));
    }

    private async Task<string> CheckDbAsync(CancellationToken cancellationToken)
    {
        if (!_factory.IsConfigured)
        {
            return "not_configured";
        }
        try
        {
            await using var connection = await _factory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return "ok";
        }
        catch (Exception)
        {
            // 探针只报状态，不泄露连接串等细节；异常细节已由框架日志记录
            return "error";
        }
    }
}
