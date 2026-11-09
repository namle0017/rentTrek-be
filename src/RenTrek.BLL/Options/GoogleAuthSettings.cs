namespace RenTrek.BLL.Options;

/// <summary>
/// Thiết lập đăng nhập Google.
/// </summary>
public class GoogleAuthSettings
{
    public const string SectionName = "GoogleAuth";

    /// <summary>OAuth Client ID của FE (trùng VITE_GOOGLE_CLIENT_ID). Token có aud khác sẽ bị từ chối.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Chỉ bật ở máy dev: cho phép mock token không có chữ ký (FE chưa cấu hình Google).
    /// Bật ở production = ai cũng đăng nhập được bằng email bất kỳ.
    /// </summary>
    public bool AllowUnsignedDevTokens { get; set; }
}
