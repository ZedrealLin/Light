using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>情绪日记（api-spec §5）。默认鉴权；正文仅本人可见，落库为密文。</summary>
[ApiController]
[Route("api/diaries")]
public sealed class DiaryController : ControllerBase
{
    private readonly DiaryService _service;

    public DiaryController(DiaryService service)
    {
        _service = service;
    }

    private long UserId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少用户身份"));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _service.ListAsync(UserId, from, to, page, pageSize, cancellationToken);
        var data = new { items, total, page, pageSize };
        return Ok(data.ToOk(HttpContext));
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Create(
        [FromBody] DiaryUpsertRequest request,
        [FromServices] IValidator<DiaryUpsertRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        try
        {
            var detail = await _service.CreateAsync(UserId, request, cancellationToken);
            return Ok(detail.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var detail = await _service.GetAsync(UserId, id, cancellationToken);
        return detail is null
            ? BadRequest(ApiResponse<object>.Fail(ErrorCodes.DiaryNotFound, "日记不存在", HttpContext.TraceIdentifier))
            : Ok(detail.ToOk(HttpContext));
    }

    [HttpPut("{id:long}")]
    [Consumes("application/json")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] DiaryUpsertRequest request,
        [FromServices] IValidator<DiaryUpsertRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        try
        {
            var detail = await _service.UpdateAsync(UserId, id, request, cancellationToken);
            return Ok(detail.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.SoftDeleteAsync(UserId, id, cancellationToken);
            return Ok(new { done = true }.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("trend")]
    public async Task<IActionResult> Trend([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 90);
        var trend = await _service.TrendAsync(UserId, days, cancellationToken);
        return Ok(trend.ToOk(HttpContext));
    }

    [HttpGet("reminder")]
    public async Task<IActionResult> GetReminder(CancellationToken cancellationToken)
    {
        var reminder = await _service.GetReminderAsync(UserId, cancellationToken)
            ?? new DiaryReminderResponse(false, "20:00", "1,2,3,4,5,6,7");
        return Ok(reminder.ToOk(HttpContext));
    }

    [HttpPut("reminder")]
    [Consumes("application/json")]
    public async Task<IActionResult> PutReminder(
        [FromBody] DiaryReminderRequest request,
        [FromServices] IValidator<DiaryReminderRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        await _service.UpsertReminderAsync(UserId, request, cancellationToken);
        return Ok(new { done = true }.ToOk(HttpContext));
    }
}
