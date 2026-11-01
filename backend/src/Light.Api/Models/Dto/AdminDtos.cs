using FluentValidation;

namespace Light.Api.Models.Dto;

public sealed record AdminLoginRequest(string Username, string Password);

public sealed record AdminTokenResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string DisplayName);

public sealed record HandleAlertRequest(string Action, string? Note);

public sealed record AdminAlertItemResponse(
    long Id,
    string Source,
    string Status,
    string? DetailJson,
    long? UserId,
    string? ExternalRef,
    DateTime CreatedAt,
    DateTime? HandledAt,
    string? HandleNote);

public sealed record AdminUserItemResponse(
    long Id,
    string PublicId,
    string? Nickname,
    string? PhoneMasked,
    string? EmailMasked,
    string Status,
    DateTime CreatedAt);

public sealed record ScaleConfigUpdateRequest(string Code, string Name, string ConfigJson, bool IsActive);

public sealed record KeywordAddRequest(string Keyword);

public sealed record KeywordToggleRequest(string Keyword, bool IsActive);

public sealed record ScaleActiveRequest(bool IsActive);

public sealed class AdminLoginRequestValidator : AbstractValidator<AdminLoginRequest>
{
    public AdminLoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("用户名不能为空");
        RuleFor(x => x.Password).NotEmpty().WithMessage("密码不能为空");
    }
}

public sealed class HandleAlertRequestValidator : AbstractValidator<HandleAlertRequest>
{
    private static readonly string[] Allowed = ["processing", "resolved", "dismissed"];

    public HandleAlertRequestValidator()
    {
        RuleFor(x => x.Action)
            .Must(a => Allowed.Contains(a)).WithMessage("action 只能是 processing/resolved/dismissed");
        RuleFor(x => x.Note).MaximumLength(1024).WithMessage("处理记录过长");
    }
}

public sealed class ScaleConfigUpdateRequestValidator : AbstractValidator<ScaleConfigUpdateRequest>
{
    public ScaleConfigUpdateRequestValidator()
    {
        RuleFor(x => x.Code).Matches("^[a-z0-9_]{1,32}$").WithMessage("量表代码格式不正确");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128).WithMessage("量表名称不能为空且不超过 128 字");
        RuleFor(x => x.ConfigJson).NotEmpty().WithMessage("配置不能为空");
    }
}

public sealed class KeywordAddRequestValidator : AbstractValidator<KeywordAddRequest>
{
    public KeywordAddRequestValidator()
    {
        RuleFor(x => x.Keyword)
            .NotEmpty().WithMessage("关键词不能为空")
            .MaximumLength(64).WithMessage("关键词最长 64 字");
    }
}
