using FluentValidation;

namespace Light.Api.Models.Dto;

// ---------- 请求 ----------

public sealed record RegisterRequest(
    string Account,
    string Password,
    string? Nickname,
    bool PrivacyConsent);

public sealed record LoginRequest(string Account, string Password);

public sealed record SmsCodeRequest(string Phone);

public sealed record SmsLoginRequest(string Phone, string Code);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

// ---------- 响应 ----------

public sealed record UserBriefResponse(string PublicId, string? Nickname, string? PhoneMasked, string? EmailMasked, DateTime? PrivacyConsentAt);

public sealed record TokenResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, UserBriefResponse User);

// ---------- 验证 ----------

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Account)
            .NotEmpty().WithMessage("账号不能为空")
            .Must(a => a.Contains('@')
                ? System.Text.RegularExpressions.Regex.IsMatch(a, @"^[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,}$")
                : System.Text.RegularExpressions.Regex.IsMatch(a, @"^1\d{10}$"))
            .WithMessage("账号需为合法手机号或邮箱");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不能为空")
            .MinimumLength(8).WithMessage("密码至少 8 位")
            .MaximumLength(64).WithMessage("密码过长");
        RuleFor(x => x.PrivacyConsent)
            .Equal(true).WithMessage("必须单独勾选同意隐私政策（硬性规则 4）");
        RuleFor(x => x.Nickname).MaximumLength(64).WithMessage("昵称过长");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Account).NotEmpty().WithMessage("账号不能为空");
        RuleFor(x => x.Password).NotEmpty().WithMessage("密码不能为空");
    }
}

public sealed class SmsCodeRequestValidator : AbstractValidator<SmsCodeRequest>
{
    public SmsCodeRequestValidator()
    {
        RuleFor(x => x.Phone).Matches(@"^1\d{10}$").WithMessage("手机号格式不正确");
    }
}

public sealed class SmsLoginRequestValidator : AbstractValidator<SmsLoginRequest>
{
    public SmsLoginRequestValidator()
    {
        RuleFor(x => x.Phone).Matches(@"^1\d{10}$").WithMessage("手机号格式不正确");
        RuleFor(x => x.Code).Matches(@"^\d{6}$").WithMessage("验证码为 6 位数字");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("refreshToken 不能为空");
    }
}
