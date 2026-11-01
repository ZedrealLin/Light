using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>后台管理接口（api-spec §9）。除登录外全部需要 admin 角色。</summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _service;

    public AdminController(AdminService service)
    {
        _service = service;
    }

    private long AdminId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少管理员身份"));

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] AdminLoginRequest request,
        [FromServices] IValidator<AdminLoginRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        return Ok((await _service.LoginAsync(request, cancellationToken)).ToOk(HttpContext));
    }

    // ---------- 预警工单 ----------

    [HttpGet("crisis-alerts")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Alerts(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = await _service.ListAlertsAsync(status, page, pageSize, cancellationToken);
        return Ok(new { items, page, pageSize }.ToOk(HttpContext));
    }

    [HttpPost("crisis-alerts/{id:long}/handle")]
    [Authorize(Roles = "admin")]
    [Consumes("application/json")]
    public async Task<IActionResult> Handle(
        long id,
        [FromBody] HandleAlertRequest request,
        [FromServices] IValidator<HandleAlertRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        var ok = await _service.HandleAlertAsync(AdminId, id, request, cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidStateTransition, "工单不存在或已处于终态", HttpContext.TraceIdentifier));
    }

    // ---------- 用户 ----------

    [HttpGet("users")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Users(
        [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = await _service.ListUsersAsync(query, page, pageSize, cancellationToken);
        return Ok(new { items, page, pageSize }.ToOk(HttpContext));
    }

    [HttpPost("users/{id:long}/disable")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Disable(long id, CancellationToken cancellationToken)
    {
        var ok = await _service.DisableUserAsync(id, disable: true, cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "用户不存在", HttpContext.TraceIdentifier));
    }

    [HttpPost("users/{id:long}/enable")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Enable(long id, CancellationToken cancellationToken)
    {
        var ok = await _service.DisableUserAsync(id, disable: false, cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "用户不存在", HttpContext.TraceIdentifier));
    }

    // ---------- 量表 ----------

    [HttpPost("scales")]
    [Authorize(Roles = "admin")]
    [Consumes("application/json")]
    public async Task<IActionResult> AddScale(
        [FromBody] ScaleConfigUpdateRequest request,
        [FromServices] IValidator<ScaleConfigUpdateRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        try
        {
            var id = await _service.AddScaleVersionAsync(request, cancellationToken);
            return Ok(new { id }.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("scales/{id:long}/active")]
    [Authorize(Roles = "admin")]
    [Consumes("application/json")]
    public async Task<IActionResult> SetScaleActive(long id, [FromBody] ScaleActiveRequest request, CancellationToken cancellationToken)
    {
        var ok = await _service.SetScaleActiveAsync(id, request.IsActive, cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "量表不存在", HttpContext.TraceIdentifier));
    }

    // ---------- 危机关键词 ----------

    [HttpGet("crisis-keywords")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Keywords([FromQuery] bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var keywords = await _service.ListKeywordsAsync(onlyActive, cancellationToken);
        return Ok(keywords.ToOk(HttpContext));
    }

    [HttpPost("crisis-keywords")]
    [Authorize(Roles = "admin")]
    [Consumes("application/json")]
    public async Task<IActionResult> AddKeyword(
        [FromBody] KeywordAddRequest request,
        [FromServices] IValidator<KeywordAddRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        await _service.AddKeywordAsync(request.Keyword, cancellationToken);
        return Ok(new { done = true }.ToOk(HttpContext));
    }

    [HttpPost("crisis-keywords/toggle")]
    [Authorize(Roles = "admin")]
    [Consumes("application/json")]
    public async Task<IActionResult> ToggleKeyword(
        [FromBody] KeywordToggleRequest request,
        [FromServices] IValidator<KeywordAddRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(new KeywordAddRequest(request.Keyword), cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        var ok = await _service.ToggleKeywordAsync(request.Keyword, request.IsActive, cancellationToken);
        return ok
            ? Ok(new { done = true }.ToOk(HttpContext))
            : BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "关键词不存在", HttpContext.TraceIdentifier));
    }
}
