using System.Security.Cryptography;

namespace RenTrek.BLL.Options;

/// <summary>
/// Chứa khóa mã hóa dữ liệu tài khoản nhận hoàn. Đổi khóa làm payload cũ không giải mã được.
/// </summary>
public class RefundSettings
{
    /// <summary>Khóa AES-256 dạng Base64 (32 byte), đọc từ cấu hình <c>RefundDataEncryptionKey</c>.</summary>
    public string DataEncryptionKey { get; set; } = string.Empty;

    /// <summary>Số ngày giữ dữ liệu tài khoản sau khi hồ sơ kết thúc.</summary>
    public int PayoutRetentionDays { get; set; } = 30;

    public bool HasValidEncryptionKey()
    {
        if (string.IsNullOrWhiteSpace(DataEncryptionKey))
            return false;

        byte[]? key = null;
        try
        {
            key = Convert.FromBase64String(DataEncryptionKey);
            return key.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
        finally
        {
            if (key is not null)
                CryptographicOperations.ZeroMemory(key);
        }
    }
}
