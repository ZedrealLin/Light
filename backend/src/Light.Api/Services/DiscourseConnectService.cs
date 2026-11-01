using System.Security.Cryptography;
using System.Text;
using Light.Api.Common;
using Microsoft.Extensions.Configuration;

namespace Light.Api.Services;

/// <summary>
/// DiscourseConnect（原 Discourse SSO）协议实现：
/// 1. Discourse 跳转过来：?sso=&lt;base64(nonce=..&amp;return_sso_url=..)&gt;&amp;sig=&lt;HMAC-SHA256(sso, secret) hex&gt;
/// 2. 校验签名 → 组装用户信息 payload → base64 → 计算 sig → 302 回 return_sso_url。
/// 密钥只走环境变量 DISCOURSE_SSO_SECRET（硬性规则 9）；功能开关 DISCOURSE_SSO_ENABLED，
/// 未启用时返回 503 引导文案（本站与 Discourse 部署为独立阶段，见 deploy/discourse）。
/// </summary>
public sealed class DiscourseConnectService
{
    public sealed record SsoRequest(string Nonce, string ReturnSsoUrl);

    public sealed record SsoUser(long UserId, string PublicId, string? Email, string? Nickname);

    private readonly byte[] _secret;
    public bool Enabled { get; }

    public DiscourseConnectService(IConfiguration configuration)
    {
        var secret = configuration["DISCOURSE_SSO_SECRET"]?.Trim();
        Enabled = bool.TryParse(configuration["DISCOURSE_SSO_ENABLED"], out var enabled) && enabled;
        _secret = string.IsNullOrWhiteSpace(secret) ? [] : Encoding.UTF8.GetBytes(secret);
    }

    /// <summary>校验 Discourse 传来的签名并解析 nonce；失败抛 BizException(1000)。</summary>
    public SsoRequest VerifyIncoming(string sso, string sig)
    {
        if (!Enabled || _secret.Length == 0)
        {
            throw new BizException(ErrorCodes.NotFound, "社区登录尚未启用");
        }
        var expected = HmacHex(sso);
        if (!FixedTimeEqualsHex(expected, sig))
        {
            throw new BizException(ErrorCodes.InvalidArgument, "SSO 签名校验失败");
        }
        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(sso));
        }
        catch (FormatException)
        {
            throw new BizException(ErrorCodes.InvalidArgument, "SSO 载荷不合法");
        }
        var nonce = ParseField(decoded, "nonce");
        var returnUrl = ParseField(decoded, "return_sso_url");
        if (string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(returnUrl))
        {
            throw new BizException(ErrorCodes.InvalidArgument, "SSO 载荷缺少 nonce 或 return_sso_url");
        }
        // return_sso_url 只允许回跳到 Discourse 自身（防开放重定向），由部署配置域名前缀校验
        return new SsoRequest(nonce, returnUrl);
    }

    /// <summary>生成回跳 Discourse 的 sso/sig 对。</summary>
    public (string Sso, string Sig) BuildResponse(SsoRequest request, SsoUser user)
    {
        var pairs = new List<string>
        {
            $"nonce={UrlEncode(request.Nonce)}",
            $"return_sso_url={UrlEncode(request.ReturnSsoUrl)}",
            $"external_id={user.UserId}",
            $"email={UrlEncode(user.Email ?? $"user{user.UserId}@example.invalid")}",
            $"username={UrlEncode(user.Nickname ?? $"user{user.PublicId[..8]}")}",
            $"name={UrlEncode(user.Nickname ?? "")}",
            $"require_activation=false",
        };
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("&", pairs)));
        return (payload, HmacHex(payload));
    }

    private static string ParseField(string decoded, string key)
    {
        foreach (var pair in decoded.Split('&'))
        {
            var idx = pair.IndexOf('=');
            if (idx > 0 && pair[..idx] == key)
            {
                return Uri.UnescapeDataString(pair[(idx + 1)..].Replace('+', ' '));
            }
        }
        return string.Empty;
    }

    private static string UrlEncode(string value) => Uri.EscapeDataString(value);

    private string HmacHex(string data) =>
        Convert.ToHexString(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    private static bool FixedTimeEqualsHex(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(actual) || actual.Length != expected.Length)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual.ToLowerInvariant()));
    }
}
