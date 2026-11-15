namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body tạo / sửa biến thể (§13.6, §13.7). SKU duy nhất toàn hệ thống.</summary>
public sealed class SaveVariantRequest
{
    public string Sku { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = "Active";

    /// <summary>Giá trị thiết bị (VND) dùng để bồi thường khi mất / hư không dùng lại được; null = chưa nhập.</summary>
    public decimal? ReplacementValue { get; set; }
}
