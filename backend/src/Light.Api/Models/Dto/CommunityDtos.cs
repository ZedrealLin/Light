using FluentValidation;

namespace Light.Api.Models.Dto;

/// <summary>社区（Discourse）发帖回调请求体。</summary>
public sealed record CommunityCallbackRequest(
    long TopicId,
    long PostId,
    string? Username,
    string? ExternalUserId,
    string Content);

public sealed class CommunityCallbackRequestValidator : AbstractValidator<CommunityCallbackRequest>
{
    public CommunityCallbackRequestValidator()
    {
        RuleFor(x => x.TopicId).GreaterThan(0).WithMessage("topicId 不合法");
        RuleFor(x => x.PostId).GreaterThan(0).WithMessage("postId 不合法");
        RuleFor(x => x.Content).NotEmpty().WithMessage("内容不能为空").MaximumLength(50000).WithMessage("内容过长");
        RuleFor(x => x.ExternalUserId).MaximumLength(64).WithMessage("externalUserId 过长");
    }
}
