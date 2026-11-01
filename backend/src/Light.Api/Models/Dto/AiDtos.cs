using FluentValidation;

namespace Light.Api.Models.Dto;

public sealed record AiSessionItem(long Id, string? Title, string Status, DateTime StartedAt);

public sealed record AiSessionCreatedResponse(long SessionId, string Disclaimer);

public sealed record AiMessageItem(long Id, string Role, string Content, DateTime CreatedAt);

public sealed record AiMessagesResponse(long SessionId, IReadOnlyList<AiMessageItem> Messages, string Disclaimer);

/// <summary>正常回复。</summary>
public sealed record AiReplyResponse(long SessionId, long MessageId, string Reply);

/// <summary>命中危机：HTTP 200 + code=7101 + 固定安全话术（不调用模型）。</summary>
public sealed record AiCrisisResponse(long SessionId, long MessageId, string SafeReply, string HelpUrl);

public sealed record AiSendMessageRequest(string Content);

public sealed class AiSendMessageRequestValidator : AbstractValidator<AiSendMessageRequest>
{
    public AiSendMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("消息不能为空")
            .MaximumLength(4000).WithMessage("单条消息最长 4000 字");
    }
}
