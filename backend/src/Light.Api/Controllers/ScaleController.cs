using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>
/// 量表与作答接口（api-spec §2）。匿名可用（硬性规则：量表作答为匿名接口）。
/// 所有响应均为统一返回体；命中预警时 data.crisis 非空（前端据此展示求助页）。
/// </summary>
[ApiController]
[Route("api/scales")]
public sealed class ScaleController : ControllerBase
{
    private readonly ScaleService _service;
    private readonly IValidator<ScaleSubmitRequest> _submitValidator;

    public ScaleController(ScaleService service, IValidator<ScaleSubmitRequest> submitValidator)
    {
        _service = service;
        _submitValidator = submitValidator;
    }

    /// <summary>启用中的量表列表（概要，不含题目明细）。匿名。</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await _service.ListAsync(cancellationToken);
        return Ok(items.ToOk(HttpContext));
    }

    /// <summary>量表详情（对外投影：题目与选项；不含计分/分级/预警规则）。匿名。</summary>
    [HttpGet("{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> Detail(string code, CancellationToken cancellationToken)
    {
        var detail = await _service.GetDetailAsync(code, cancellationToken);
        return detail is null
            ? BadRequest(ApiResponse<object>.Fail(ErrorCodes.ScaleNotFound, "量表不存在或未启用", HttpContext.TraceIdentifier))
            : Ok(detail.ToOk(HttpContext));
    }

    /// <summary>提交作答（匿名；可带 token 由鉴权层关联 user）。命中预警 → data.crisis 非空。</summary>
    [HttpPost("/api/scale-submissions")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> Submit(
        [FromBody] ScaleSubmitRequest request,
        [FromServices] ScaleService service,
        CancellationToken cancellationToken)
    {
        var validation = await _submitValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var message = validation.Errors[0].ErrorMessage;
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, message, HttpContext.TraceIdentifier));
        }

        try
        {
            // 当前为匿名作答：登录态在阶段 2 接入后通过 HttpContext 关联 user_id
            var response = await service.SubmitAsync(userId: null, request, cancellationToken);
            return Ok(response.ToOk(HttpContext));
        }
        catch (ScaleNotFoundException)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ScaleNotFound, "量表不存在或未启用", HttpContext.TraceIdentifier));
        }
        catch (ScaleAnswerInvalidException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.ScaleAnswerInvalid, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    /// <summary>按 public_id 取回作答结果（匿名取回，结果非敏感明文）。匿名。</summary>
    [HttpGet("/api/scale-submissions/{publicId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Result(string publicId, CancellationToken cancellationToken)
    {
        var result = await _service.GetResultAsync(publicId, cancellationToken);
        return result is null
            ? BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "作答记录不存在", HttpContext.TraceIdentifier))
            : Ok(result.ToOk(HttpContext));
    }
}
