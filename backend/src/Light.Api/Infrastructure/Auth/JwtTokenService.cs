using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Light.Api.Infrastructure.Auth;

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// 访问令牌签发（JWT Bearer）。密钥只来自配置 Jwt:Secret（环境变量 JWT_SECRET 注入，硬性规则 9）。
/// 刷新令牌不使用 JWT：为不透明随机串，仅存 SHA-256 哈希（refresh_tokens 表）。
/// </summary>
public sealed class JwtTokenService
{
    private readonly byte[] _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        // 兼容两种注入方式：环境变量 JWT_SECRET（CONVENTIONS 4.5 命名）或配置键 Jwt:Secret（Jwt__Secret）
        var secret = (configuration["JWT_SECRET"] ?? configuration["Jwt:Secret"])?.Trim();
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("JWT 未配置：请通过环境变量 JWT_SECRET 提供至少 32 字符的密钥");
        }
        _secret = Encoding.UTF8.GetBytes(secret);
        _issuer = configuration["Jwt:Issuer"] ?? "light-api";
        _audience = configuration["Jwt:Audience"] ?? "light-clients";
        _accessMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var m) ? m : 60;
    }

    public IssuedToken IssueAccessToken(long userId, string? role = null)
    {
        var expires = DateTime.UtcNow.AddMinutes(_accessMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(new SymmetricSecurityKey(_secret), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: expires, signingCredentials: credentials);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _issuer,
        ValidateAudience = true,
        ValidAudience = _audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(_secret),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
    };

    /// <summary>刷新令牌：随机 64 字节 → base64url 原文（发给客户端）+ SHA-256 哈希（入库）。</summary>
    public static (string Plain, string Hash) NewRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plain = Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
        return (plain, hash);
    }

    public static string HashRefreshToken(string plain) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
}
