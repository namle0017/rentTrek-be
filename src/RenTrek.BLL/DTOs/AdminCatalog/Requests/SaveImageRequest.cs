namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thêm ảnh: dùng chung cho thư viện tour (§6.8) và ảnh biến thể (§13.9).</summary>
public sealed class SaveImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
