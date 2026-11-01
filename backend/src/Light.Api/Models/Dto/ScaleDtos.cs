using FluentValidation;

namespace Light.Api.Models.Dto;

// ---------- 响应 ----------

public sealed record ScaleListItemResponse(
    string Code,
    string Name,
    string? Description,
    int QuestionCount,
    int EstimatedMinutes,
    string? SourceName,
    bool SourceVerified);

public sealed record ScaleOptionResponse(int Value, string Label);

public sealed record ScaleQuestionResponse(int No, string Title, IReadOnlyList<ScaleOptionResponse> Options);

/// <summary>量表详情（对外投影）：绝不含 scoring / levels / crisisRules——计分与预警规则是配置机密（防作弊）。</summary>
public sealed record ScaleDetailResponse(
    string Code,
    string Name,
    string? Description,
    string? Instructions,
    string? SourceName,
    bool SourceVerified,
    string? Disclaimer,
    IReadOnlyList<ScaleQuestionResponse> Questions);

public sealed record SubmissionCrisisResponse(string HelpUrl);

public sealed record ScaleSubmissionResponse(
    string SubmissionPublicId,
    int TotalScore,
    string LevelCode,
    string LevelLabel,
    string Disclaimer,
    SubmissionCrisisResponse? Crisis);

// ---------- 请求 ----------

public sealed record ScaleAnswerItem(int No, int Value);

public sealed record ScaleSubmitRequest(string ScaleCode, IReadOnlyList<ScaleAnswerItem> Answers);

/// <summary>入参验证（FluentValidation）。题目完整性 / 选项合法性由 ScaleEngine 在服务层校验（3002）。</summary>
public sealed class ScaleSubmitRequestValidator : AbstractValidator<ScaleSubmitRequest>
{
    public ScaleSubmitRequestValidator()
    {
        RuleFor(x => x.ScaleCode)
            .NotEmpty().WithMessage("量表代码不能为空")
            .Matches("^[a-z0-9_]{1,32}$").WithMessage("量表代码格式不正确");

        RuleFor(x => x.Answers)
            .NotEmpty().WithMessage("答案不能为空")
            .Must(a => a.Count <= 200).WithMessage("答案数量超出限制");

        RuleForEach(x => x.Answers)
            .Must(a => a.No >= 1 && a.No <= 1000).WithMessage("题号不合法")
            .Must(a => a.Value >= 0 && a.Value <= 10).WithMessage("选项值不合法");
    }
}
