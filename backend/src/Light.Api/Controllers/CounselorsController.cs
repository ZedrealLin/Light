using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>咨询师浏览（匿名，api-spec §7）。</summary>
[ApiController]
[Route("api/counselors")]
public sealed class CounselorsController : ControllerBase
{
    private readonly BookingRepository _bookings;

    public CounselorsController(BookingRepository bookings)
    {
        _bookings = bookings;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rows = await _bookings.ListActiveCounselorsAsync(cancellationToken);
        var items = rows.Select(c => new CounselorListItemResponse(
            c.Id, c.Name, c.Title, c.Specialties, c.AvatarUrl)).ToList();
        return Ok(items.ToOk(HttpContext));
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var c = await _bookings.FindActiveCounselorAsync(id, cancellationToken);
        if (c is null)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "咨询师不存在或未上架", HttpContext.TraceIdentifier));
        }
        // 资质编号对外脱敏展示（12****89）
        var data = new CounselorDetailResponse(
            c.Id, c.Name, c.Title, c.Specialties, c.Intro, c.AvatarUrl, c.ReviewStatus);
        return Ok(data.ToOk(HttpContext));
    }

    [HttpGet("{id:long}/slots")]
    [AllowAnonymous]
    public async Task<IActionResult> Slots(
        long id, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var counselor = await _bookings.FindActiveCounselorAsync(id, cancellationToken);
        if (counselor is null)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "咨询师不存在或未上架", HttpContext.TraceIdentifier));
        }
        var slots = await _bookings.ListAvailableSlotsAsync(id, null, to, cancellationToken);
        var items = slots.Select(s => new SlotItemResponse(s.Id, s.StartAt, s.EndAt)).ToList();
        return Ok(items.ToOk(HttpContext));
    }
}

/// <summary>预约（api-spec §7）：默认鉴权；创建/取消走事务防超卖。</summary>
[ApiController]
[Route("api/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly BookingRepository _bookings;

    public BookingsController(BookingRepository bookings)
    {
        _bookings = bookings;
    }

    private long UserId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少用户身份"));

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Create(
        [FromBody] BookingCreateRequest request,
        [FromServices] IValidator<BookingCreateRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        var (bookingId, errorCode) = await _bookings.CreateBookingAsync(UserId, request.SlotId, cancellationToken);
        if (errorCode != 0)
        {
            return BadRequest(ApiResponse<object>.Fail(errorCode, "该时段已被预约", HttpContext.TraceIdentifier));
        }
        var row = (await _bookings.ListByUserAsync(UserId, cancellationToken)).FirstOrDefault(b => b.Id == bookingId);
        var data = new BookingCreateResponse(
            bookingId, row?.CounselorId ?? 0, row?.CounselorName ?? "",
            row?.StartAt ?? DateTime.UtcNow, row?.EndAt ?? DateTime.UtcNow);
        return Ok(data.ToOk(HttpContext));
    }

    [HttpGet("my")]
    public async Task<IActionResult> My(CancellationToken cancellationToken)
    {
        var rows = await _bookings.ListByUserAsync(UserId, cancellationToken);
        var items = rows.Select(r => new BookingItemResponse(
            r.Id, r.CounselorId, r.CounselorName, r.StartAt, r.EndAt,
            r.Status, r.PaymentStatus, r.BookedAt, r.CancelledAt)).ToList();
        return Ok(items.ToOk(HttpContext));
    }

    [HttpPost("{id:long}/cancel")]
    [Consumes("application/json")]
    public async Task<IActionResult> Cancel(
        long id, [FromBody] BookingCancelRequest? request, CancellationToken cancellationToken)
    {
        var ok = await _bookings.CancelAsync(UserId, id, request?.Reason?.Trim(), cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.BookingInvalid, "预约不存在或状态不允许取消", HttpContext.TraceIdentifier));
    }
}
