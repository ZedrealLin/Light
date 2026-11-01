using FluentValidation;

namespace Light.Api.Models.Dto;

// ---------- 前台 ----------

public sealed record ArticleListItemResponse(string Slug, string Title, string? Summary, DateTime? PublishedAt);

public sealed record ArticleDetailResponse(
    string Slug,
    string Title,
    string Content,
    string SourceInfo,
    string ReviewerName,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    string Disclaimer);

// ---------- 后台 ----------

public sealed record ArticleAdminItemResponse(
    long Id,
    string Title,
    string Status,
    string SourceInfo,
    string? ReviewNote,
    DateTime? PublishedAt,
    DateTime UpdatedAt);

public sealed record ArticleUpsertRequest(
    long? CategoryId,
    string Title,
    string? Slug,
    string? Summary,
    string Content,
    string SourceInfo);

public sealed record ArticleReviewRequest(bool Approve, string? Note);

public sealed class ArticleUpsertRequestValidator : AbstractValidator<ArticleUpsertRequest>
{
    public ArticleUpsertRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255).WithMessage("标题不能为空且不超过 255 字");
        RuleFor(x => x.Content).NotEmpty().MinimumLength(20).WithMessage("正文不能为空（至少 20 字）");
        RuleFor(x => x.SourceInfo).NotEmpty().MaximumLength(512).WithMessage("信息来源不能为空（硬性规则 7）");
        RuleFor(x => x.Summary).MaximumLength(512).WithMessage("摘要过长");
        RuleFor(x => x.Slug).Matches("^[a-z0-9-]{0,255}$").When(x => !string.IsNullOrEmpty(x.Slug))
            .WithMessage("slug 只能是小写字母、数字与连字符");
    }
}

public sealed class ArticleReviewRequestValidator : AbstractValidator<ArticleReviewRequest>
{
    public ArticleReviewRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(512).WithMessage("审核意见过长");
        // 驳回必须给理由
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Approve).WithMessage("驳回时必须填写审核意见");
    }
}
