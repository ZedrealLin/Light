using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Light.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>科普文章（前台读取，api-spec §6）：仅已发布可见；匿名接口。</summary>
[ApiController]
[Route("api/articles")]
public sealed class ArticlesController : ControllerBase
{
    private readonly ArticleService _service;

    public ArticlesController(ArticleService service)
    {
        _service = service;
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var items = await _service.ListCategoriesAsync(cancellationToken);
        return Ok(items.Select(c => new { id = c.Id, name = c.Name, slug = c.Slug }).ToOk(HttpContext));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(
        [FromQuery] long? categoryId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = await _service.ListPublishedAsync(categoryId, page, pageSize, cancellationToken);
        return Ok(new { items, page, pageSize }.ToOk(HttpContext));
    }

    [HttpGet("{slugOrId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Detail(string slugOrId, CancellationToken cancellationToken)
    {
        var detail = await _service.GetPublishedAsync(slugOrId, cancellationToken);
        return detail is null
            ? BadRequest(ApiResponse<object>.Fail(ErrorCodes.ArticleNotFound, "文章不存在或未发布", HttpContext.TraceIdentifier))
            : Ok(detail.ToOk(HttpContext));
    }
}

/// <summary>后台文章管理（api-spec §9）：admin 角色；提交/审核/下线状态机。</summary>
[ApiController]
[Route("api/admin/articles")]
[Authorize(Roles = "admin")]
public sealed class AdminArticlesController : ControllerBase
{
    private readonly ArticleService _service;

    public AdminArticlesController(ArticleService service)
    {
        _service = service;
    }

    private long AdminId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少管理员身份"));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = await _service.ListAllAsync(status, page, pageSize, cancellationToken);
        return Ok(new { items, page, pageSize }.ToOk(HttpContext));
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Create(
        [FromBody] ArticleUpsertRequest request,
        [FromServices] IValidator<ArticleUpsertRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        var id = await _service.CreateAsync(AdminId, request, cancellationToken);
        return Ok(new { id }.ToOk(HttpContext));
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.SubmitAsync(id, cancellationToken);
            return Ok(new { done = true }.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("{id:long}/review")]
    [Consumes("application/json")]
    public async Task<IActionResult> Review(
        long id,
        [FromBody] ArticleReviewRequest request,
        [FromServices] IValidator<ArticleReviewRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        try
        {
            await _service.ReviewAsync(AdminId, id, request, cancellationToken);
            return Ok(new { done = true }.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("{id:long}/offline")]
    public async Task<IActionResult> Offline(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.OfflineAsync(id, cancellationToken);
            return Ok(new { done = true }.ToOk(HttpContext));
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }
}
