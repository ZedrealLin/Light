using System.Collections.Concurrent;
using Light.Api.Common;
using Light.Api.Infrastructure.Auth;
using Light.Api.Infrastructure.Db;
using Light.Api.Models.Dto;
using Light.Api.Repositories;

namespace Light.Api.Services;

/// <summary>业务异常：携带业务错误码，由控制器转为统一返回体。</summary>
public sealed class BizException : Exception
{
    public int Code { get; }
    public BizException(int code, string message) : base(message) => Code = code;
}

/// <summary>
/// 认证与账号业务。
/// 短信验证码为 mock 实现（内存存储，阶段 6 部署时切换 Redis 并接入真实短信通道——AGENTS 允许 mock）；
/// 验证码不在日志输出明文，Development 下随响应返回以便联调。
/// </summary>
public sealed class AuthService
{
    private static readonly ConcurrentDictionary<string, (string Code, DateTime ExpireAt)> SmsCodes = new();

    private readonly UserRepository _users;
    private readonly JwtTokenService _jwt;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;

    private static readonly TimeSpan SmsCodeTtl = TimeSpan.FromMinutes(5);
    private const int RefreshTokenDays = 14;

    public AuthService(
        UserRepository users, JwtTokenService jwt,
        IWebHostEnvironment environment, ILogger<AuthService> logger)
    {
        _users = users;
        _jwt = jwt;
        _environment = environment;
        _logger = logger;
    }

    public async Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var existing = await _users.FindByAccountAsync(request.Account, cancellationToken);
        if (existing is not null)
        {
            throw new BizException(ErrorCodes.AccountExists, "该账号已存在");
        }

        var isEmail = request.Account.Contains('@');
        var userId = await _users.InsertAsync(
            phone: isEmail ? null : request.Account,
            email: isEmail ? request.Account : null,
            passwordHash: PasswordHasher.Hash(request.Password),
            nickname: string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim(),
            privacyConsentAt: DateTime.UtcNow,
            cancellationToken);

        return await IssueTokensAsync(userId, cancellationToken);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByAccountAsync(request.Account, cancellationToken);
        if (user is null || user.PasswordHash is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new BizException(ErrorCodes.InvalidCredentials, "账号或密码错误");
        }
        if (user.Status != "active")
        {
            throw new BizException(ErrorCodes.AccountUnavailable, "账号已注销或禁用");
        }
        return await IssueTokensAsync(user.Id, cancellationToken);
    }

    /// <summary>发送验证码（mock）：仅生成与内存暂存，不调用任何真实短信通道。</summary>
    public async Task SendSmsCodeAsync(string phone, CancellationToken cancellationToken)
    {
        var existing = await _users.FindByAccountAsync(phone, cancellationToken);
        if (existing is { Status: "anonymized" })
        {
            throw new BizException(ErrorCodes.AccountUnavailable, "账号已注销");
        }

        var code = Random.Shared.Next(100000, 1000000).ToString();
        SmsCodes[phone] = (code, DateTime.UtcNow.Add(SmsCodeTtl));
        // mock：不输出验证码到日志（日志脱敏），仅在 Development 随响应返回供联调
        _logger.LogInformation("验证码已生成（mock，不入日志） phone={Phone}", phone);
        await Task.CompletedTask;
        MockLastCode = _environment.IsDevelopment() ? code : null;
    }

    /// <summary>最近一次生成的验证码；仅 Development 联调用，生产恒为 null。</summary>
    public string? MockLastCode { get; private set; }

    public async Task<TokenResponse> SmsLoginAsync(SmsLoginRequest request, CancellationToken cancellationToken)
    {
        var ok = SmsCodes.TryGetValue(request.Phone, out var entry)
                 && entry.ExpireAt > DateTime.UtcNow
                 && entry.Code == request.Code;
        SmsCodes.TryRemove(request.Phone, out _);
        if (!ok)
        {
            throw new BizException(ErrorCodes.InvalidCaptcha, "验证码无效或已过期");
        }

        var user = await _users.FindByAccountAsync(request.Phone, cancellationToken);
        long userId;
        if (user is null)
        {
            // 无账号则自动注册（未勾选隐私政策时 privacy_consent_at 为空，前端需引导补签）
            userId = await _users.InsertAsync(
                phone: request.Phone, email: null, passwordHash: null,
                nickname: null, privacyConsentAt: null, cancellationToken);
        }
        else
        {
            if (user.Status != "active")
            {
                throw new BizException(ErrorCodes.AccountUnavailable, "账号已注销或禁用");
            }
            userId = user.Id;
        }
        return await IssueTokensAsync(userId, cancellationToken);
    }

    /// <summary>刷新令牌轮换：旧 token 撤销，签发新对（检测重放：旧 token 已撤销即拒绝）。</summary>
    public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        var found = await _users.FindValidRefreshTokenAsync(hash, cancellationToken);
        if (found is null)
        {
            throw new BizException(ErrorCodes.Unauthorized, "登录状态已失效，请重新登录");
        }
        var (user, tokenId) = found.Value;
        if (user.Status != "active")
        {
            throw new BizException(ErrorCodes.AccountUnavailable, "账号已注销或禁用");
        }
        await _users.RevokeRefreshTokenAsync(hash, cancellationToken);
        return await IssueTokensAsync(user.Id, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        await _users.RevokeRefreshTokenAsync(hash, cancellationToken);
    }

    public async Task<TokenResponse> IssueTokensAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken)
            ?? throw new BizException(ErrorCodes.Unauthorized, "用户不存在");
        var access = _jwt.IssueAccessToken(userId);

        // 刷新令牌哈希入库需要写库；为避免一次额外连接，直接复用仓储
        var (plain, hash) = JwtTokenService.NewRefreshToken();
        await _users.InsertRefreshTokenAsync(userId, hash, DateTime.UtcNow.AddDays(RefreshTokenDays), cancellationToken);

        return new TokenResponse(
            access.Token,
            access.ExpiresAtUtc,
            plain,
            new UserBriefResponse(
                user.PublicId, user.Nickname,
                Mask(user.Phone), Mask(user.Email),
                user.PrivacyConsentAt));
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Contains('@')
            ? $"{value[..2]}****{value[(value.IndexOf('@') - 1)..]}"
            : $"{value[..3]}****{value[^2..]}";
    }
}
