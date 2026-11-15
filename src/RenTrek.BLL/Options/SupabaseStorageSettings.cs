namespace RenTrek.BLL.Options;

/// <summary>
/// Supabase Storage lưu ảnh upload (avatar, ảnh bài viết). Bucket phải để Public.
/// Bỏ trống Url/ServiceKey thì ảnh lưu vào wwwroot/uploads (chỉ dùng khi dev, Render xóa đĩa mỗi lần deploy).
/// </summary>
public class SupabaseStorageSettings
{
    public const string SectionName = "Supabase";

    /// <summary>Ví dụ https://abcd.supabase.co</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>service_role key, chỉ để ở BE, không bao giờ gửi xuống FE.</summary>
    public string ServiceKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = "uploads";

    /// <summary>Bucket PRIVATE chứa chứng từ hoàn tiền; BE tải về bằng service key rồi stream cho người có quyền.</summary>
    public string PrivateBucket { get; set; } = "refund-proofs";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(ServiceKey);
}
