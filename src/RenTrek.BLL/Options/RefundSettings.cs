namespace RenTrek.BLL.Options;

/// <summary>
/// Chứa khóa mã hóa dữ liệu tài khoản nhận hoàn. Đổi khóa làm payload cũ không giải mã được.
/// </summary>
public class RefundSettings
{
    /// <summary>Khóa AES-256 dạng Base64 (32 byte), đọc từ cấu hình <c>RefundDataEncryptionKey</c>.</summary>
    public string DataEncryptionKey { get; set; } = string.Empty;
}
