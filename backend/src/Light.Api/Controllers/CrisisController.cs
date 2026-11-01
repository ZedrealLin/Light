using System.Text.Json;
using System.Text.Json.Serialization;
using Light.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>
/// 危机求助页数据接口（crisis-flow §2/§5，api-spec §3）。
/// 硬性规则 2：热线号码一律可配置（CRISIS_HOTLINES），代码与配置中只有占位符，标注"上线前人工核实"。
/// </summary>
[ApiController]
[Route("api/crisis")]
public sealed class CrisisController : ControllerBase
{
    private const string DefaultHotlinesJson =
        """[{"name":"心理援助热线（占位，上线前人工核实）","number":"0-000-000000","note":"上线前人工核实（docs/TODO.md T001）"}]""";

    private readonly IConfiguration _configuration;
    private readonly Services.CrisisDetectionService _detection;
    private readonly ILogger<CrisisController> _logger;

    public CrisisController(
        IConfiguration configuration,
        Services.CrisisDetectionService detection,
        ILogger<CrisisController> logger)
    {
        _configuration = configuration;
        _detection = detection;
        _logger = logger;
    }

    public sealed record Hotline(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("note")] string? Note);

    public sealed record CrisisHelpResponse(
        string Disclaimer,
        IReadOnlyList<Hotline> Hotlines,
        string Advice,
        string HelpUrl);

    /// <summary>求助页数据（匿名，主要页面常驻入口指向 /crisis-help）。</summary>
    [HttpGet("help")]
    [AllowAnonymous]
    public IActionResult Help()
    {
        var data = new CrisisHelpResponse(
            Disclaimer: "你不是一个人在面对。本页面内容仅为求助引导，不构成医疗建议；如有紧急情况，请立即拨打热线或前往就近医院。",
            Hotlines: LoadHotlines(),
            Advice: "如果你此刻有伤害自己的想法：请先保证自己处于安全的环境，拨打下方热线或告诉身边可信任的人；也可以使用本站的自评量表了解自己的状态，并考虑咨询专业人员。",
            HelpUrl: Services.ScaleService.CrisisHelpUrl);

        return Ok(data.ToOk(HttpContext));
    }

    private IReadOnlyList<Hotline> LoadHotlines()
    {
        var json = _configuration["CRISIS_HOTLINES"];
        if (string.IsNullOrWhiteSpace(json))
        {
            json = DefaultHotlinesJson;
        }
        try
        {
            var hotlines = JsonSerializer.Deserialize<List<Hotline>>(json);
            if (hotlines is { Count: > 0 })
            {
                return hotlines;
            }
        }
        catch (JsonException ex)
        {
            // 配置错误时回退占位符，绝不让求助页空白
            _logger.LogError(ex, "CRISIS_HOTLINES 配置解析失败，已回退占位热线");
        }
        return JsonSerializer.Deserialize<List<Hotline>>(DefaultHotlinesJson)!;
    }
}
