using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>后台：咨询师档案（含资质，直接审核通过入档）、排班、预约管理（api-spec §9）。</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminBookingController : ControllerBase
{
    private readonly BookingRepository _bookings;

    public AdminBookingController(BookingRepository bookings)
    {
        _bookings = bookings;
    }

    private long AdminId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少管理员身份"));

    [HttpPost("counselors")]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateCounselor(
        [FromBody] CounselorCreateRequest request,
        [FromServices] IValidator<CounselorCreateRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        var id = await _bookings.InsertCounselorAsync(
            request.Name.Trim(), request.Gender, request.Title?.Trim(), request.LicenseNo?.Trim(),
            request.Intro?.Trim(), request.Specialties?.Trim(), AdminId, cancellationToken);
        return Ok(new { id }.ToOk(HttpContext));
    }

    [HttpPost("counselors/{id:long}/slots")]
    [Consumes("application/json")]
    public async Task<IActionResult> AddSlots(
        long id,
        [FromBody] SlotBatchCreateRequest request,
        [FromServices] IValidator<SlotBatchCreateRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        var counselor = await _bookings.FindActiveCounselorAsync(id, cancellationToken);
        if (counselor is null)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "咨询师不存在或未上架", HttpContext.TraceIdentifier));
        }
        var inserted = await _bookings.InsertSlotsAsync(
            id, request.StartAt, request.EndAt, request.Count, request.IntervalMinutes, cancellationToken);
        return Ok(new { inserted }.ToOk(HttpContext));
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings(
        [FromQuery] long? counselorId, CancellationToken cancellationToken = default)
    {
        var rows = await _bookings.ListAllBookingsAsync(counselorId, cancellationToken);
        var items = rows.Select(r => new BookingItemResponse(
            r.Id, r.CounselorId, r.CounselorName, r.StartAt, r.EndAt,
            r.Status, r.PaymentStatus, r.BookedAt, r.CancelledAt)).ToList();
        return Ok(items.ToOk(HttpContext));
    }

    [HttpPost("bookings/{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, CancellationToken cancellationToken)
    {
        var ok = await _bookings.CompleteBookingAsync(id, "completed", cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.BookingInvalid, "预约不存在或状态不允许该操作", HttpContext.TraceIdentifier));
    }

    [HttpPost("bookings/{id:long}/no-show")]
    public async Task<IActionResult> NoShow(long id, CancellationToken cancellationToken)
    {
        var ok = await _bookings.CompleteBookingAsync(id, "no_show", cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.BookingInvalid, "预约不存在或状态不允许该操作", HttpContext.TraceIdentifier));
    }
}
