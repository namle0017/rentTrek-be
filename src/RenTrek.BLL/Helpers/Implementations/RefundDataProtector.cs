using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Mã hóa số tài khoản bằng AES-256-GCM trước khi ghi DB. AAD gắn payload với đúng hồ sơ, chép sang hồ sơ khác sẽ giải mã thất bại.
/// </summary>
public sealed class RefundDataProtector(IOptions<RefundSettings> options) : IRefundDataProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public ProtectedRefundPayload Protect(RefundPayoutDetails details, Guid refundCaseId, Guid orderId)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(details);
        var ciphertext = new byte[plaintext.Length];
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var key = Key();

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Aad(refundCaseId, orderId));
            return new ProtectedRefundPayload(ciphertext, nonce, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public RefundPayoutDetails Unprotect(ProtectedRefundPayload payload, Guid refundCaseId, Guid orderId)
    {
        var plaintext = new byte[payload.Ciphertext.Length];
        var key = Key();

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext, Aad(refundCaseId, orderId));
            return JsonSerializer.Deserialize<RefundPayoutDetails>(plaintext)
                ?? throw new CryptographicException("Payload hoàn tiền không hợp lệ");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] Aad(Guid refundCaseId, Guid orderId)
        => Encoding.UTF8.GetBytes($"{refundCaseId}:{orderId}");

    private byte[] Key()
    {
        try
        {
            var key = Convert.FromBase64String(options.Value.DataEncryptionKey);
            if (key.Length == KeySize)
                return key;
            CryptographicOperations.ZeroMemory(key);
        }
        catch (FormatException)
        {
            // Cùng thông báo cấu hình ở dưới, không để lộ giá trị khóa
        }

        throw new InvalidOperationException("RefundDataEncryptionKey phải là chuỗi Base64 của 32 byte");
    }
}
