using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Relay.Delivery.Application;
using Relay.Delivery.Contracts;

namespace Relay.Delivery.Infrastructure;

/// <summary>AES-GCM: nonce (12 bytes) + tag (16 bytes) + ciphertext. The message ID is authenticated as associated data.</summary>
internal sealed class AesContentProtector(IOptions<DeliveryOptions> options) : IContentProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = Convert.FromBase64String(options.Value.ContentKey);

    public static bool IsValidKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return false;
        }

        var buffer = new byte[64];
        return Convert.TryFromBase64String(base64, buffer, out var length) && length is 16 or 24 or 32;
    }

    public byte[] Protect(string plaintext, string associatedData)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[NonceSize + TagSize + plain.Length];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize), Encoding.UTF8.GetBytes(associatedData));
        return output;
    }

    public string Unprotect(byte[] protectedBytes, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(protectedBytes);
        var plain = new byte[protectedBytes.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            protectedBytes.AsSpan(0, NonceSize),
            protectedBytes.AsSpan(NonceSize + TagSize),
            protectedBytes.AsSpan(NonceSize, TagSize),
            plain,
            Encoding.UTF8.GetBytes(associatedData));
        return Encoding.UTF8.GetString(plain);
    }
}
