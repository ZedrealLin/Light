using Light.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>
/// 健康检查（匿名）：部署探针与本地自检用。
/// db / redis 组件状态在 1.2 / 后续步骤接入后如实上报，未接入前标 pending。
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var data = new
        {
            status = "ok",
            components = new
            {
                api = "ok",
                db = "pending",
                redis = "pending",
            },
        };
        return Ok(data.ToOk(HttpContext));
    }
}
