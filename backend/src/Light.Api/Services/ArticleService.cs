using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Light.Api.Services;

namespace Light.Api.Services;

/// <summary>
/// 科普文章业务。审核流：draft → pending_review → published/draft（驳回）；offline 下线。
/// 前台仅 published 可见；页面展示 信息来源 / 审核人 / 更新时间（硬性规则 7）。
/// </summary>
public sealed class ArticleService
{
    private readonly ArticleRepository _articles;
    private readonly ILogger<ArticleService> _logger;

    public ArticleService(ArticleRepository articles, ILogger<ArticleService> logger)
    {
        _articles = articles;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ArticleCategoryRow>> ListCategoriesAsync(CancellationToken cancellationToken)
        => await _articles.ListCategoriesAsync(cancellationToken);

    public async Task<IReadOnlyList<ArticleListItemResponse>> ListPublishedAsync(
        long? categoryId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (rows, _) = await _articles.ListPublishedAsync(categoryId, page, pageSize, cancellationToken);
        return rows.Select(r => new ArticleListItemResponse(
            r.Slug ?? $"id-{r.Id}", r.Title, r.Summary, r.PublishedAt)).ToList();
    }

    public async Task<ArticleDetailResponse?> GetPublishedAsync(string slugOrId, CancellationToken cancellationToken)
    {
        ArticleRow? row;
        if (long.TryParse(slugOrId, out var numericId))
        {
            var meta = await _articles.GetMetaForPublishedAsync(numericId, cancellationToken);
            var list = await _articles.ListPublishedAsync(null, 1, 1000, cancellationToken);
            row = list.Items.FirstOrDefault(a => a.Id == numericId);
            if (row is null || meta is null)
            {
                return null;
            }
            return ToDetail(row, meta.Value.ReviewerName, meta.Value.UpdatedAt);
        }
        row = await _articles.FindPublishedBySlugAsync(slugOrId, cancellationToken);
        if (row is null)
        {
            return null;
        }
        var meta2 = await _articles.GetMetaForPublishedAsync(row.Id, cancellationToken);
        return ToDetail(row, meta2?.ReviewerName ?? "后台", meta2?.UpdatedAt ?? row.UpdatedAt);
    }

    private static ArticleDetailResponse ToDetail(ArticleRow row, string reviewer, DateTime updatedAt) => new(
        row.Slug ?? $"id-{row.Id}",
        row.Title,
        row.Content,
        row.SourceInfo,
        reviewer,
        row.PublishedAt,
        updatedAt,
        "本站内容仅为科普信息，不构成医疗建议；如有需要请咨询专业人员。");

    // ---------- 后台 ----------

    public async Task<IReadOnlyList<ArticleAdminItemResponse>> ListAllAsync(
        string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (rows, _) = await _articles.ListAllAsync(status, page, pageSize, cancellationToken);
        return rows.Select(r => new ArticleAdminItemResponse(
            r.Id, r.Title, r.Status, r.SourceInfo, r.ReviewNote, r.PublishedAt, r.UpdatedAt)).ToList();
    }

    public async Task<long> CreateAsync(
        long adminId, ArticleUpsertRequest request, CancellationToken cancellationToken)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? $"a{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.Slug.Trim();
        return await _articles.InsertAsync(
            request.CategoryId, request.Title.Trim(), slug, request.Summary?.Trim(),
            request.Content, request.SourceInfo.Trim(), adminId, cancellationToken);
    }

    public async Task SubmitAsync(long id, CancellationToken cancellationToken)
    {
        var ok = await _articles.SubmitAsync(id, cancellationToken);
        if (!ok)
        {
            throw new BizException(ErrorCodes.InvalidStateTransition, "文章不存在或当前状态不允许提交审核");
        }
    }

    public async Task ReviewAsync(long adminId, long id, ArticleReviewRequest request, CancellationToken cancellationToken)
    {
        var ok = await _articles.ReviewAsync(id, adminId, request.Approve, request.Note?.Trim(), cancellationToken);
        if (!ok)
        {
            throw new BizException(ErrorCodes.InvalidStateTransition, "文章不存在或不在待审核状态");
        }
        _logger.LogInformation("文章审核完成 articleId={ArticleId} approve={Approve} admin={AdminId}", id, request.Approve, adminId);
    }

    public async Task OfflineAsync(long id, CancellationToken cancellationToken)
    {
        var ok = await _articles.SetStatusAsync(id, "offline", cancellationToken);
        if (!ok)
        {
            throw new BizException(ErrorCodes.NotFound, "文章不存在");
        }
    }
}
