using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Light.Api.Infrastructure.Crypto;

/// <summary>应用层加密抽象（日记正文、量表答案、AI 消息落库前必经）。</summary>
public interface IAesCipher
{
    bool IsConfigured { get; }

    /// <summary>加密为 nonce(12B) + ciphertext + tag(16B) 的二进制载荷。</summary>
    byte[] Encrypt(string plaintext);

    /// <summary>解密二进制载荷为明文。</summary>
    string Decrypt(byte[] payload);
}

/// <summary>
/// AES-GCM 实现（System.Security.Cryptography.AesGcm）。
/// 密钥只来自环境变量 AES_DATA_KEY（32 字节、base64 编码）；硬性规则 3/9：
/// 密钥不进代码仓库、不硬编码；日志不得输出明文与密钥。
/// </summary>
public sealed class AesGcmCipher : IAesCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[]? _key;

    public AesGcmCipher(IConfiguration configuration)
    {
        // Trim：容忍环境变量末尾的空白/换行（Windows 追加变量时常带 CRLF）
        var base64 = configuration["AES_DATA_KEY"]?.Trim();
        if (string.IsNullOrWhiteSpace(base64))
        {
            _key = null;
            return;
        }
        var key = Convert.FromBase64String(base64);
        if (key.Length != KeySize)
        {
            throw new InvalidOperationException($"AES_DATA_KEY 必须是 {KeySize} 字节（base64）");
        }
        _key = key;
    }

    public bool IsConfigured => _key is not null;

    public byte[] Encrypt(string plaintext)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("加密未配置：请通过环境变量 AES_DATA_KEY 提供 32 字节 base64 密钥");
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, payload, NonceSize + ciphertext.Length, TagSize);
        return payload;
    }

    public string Decrypt(byte[] payload)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("加密未配置：请通过环境变量 AES_DATA_KEY 提供 32 字节 base64 密钥");
        }
        if (payload.Length < NonceSize + TagSize)
        {
            throw new InvalidOperationException("密文载荷长度不合法");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);
        var plaintextBytes = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
