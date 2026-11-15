namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body tạo / sửa combo (§13.15, §13.17). Status: Active | Inactive | Draft (Draft lưu như Inactive).</summary>
public sealed class SaveBundleRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "Draft";

    /// <summary>Giá trị combo (VND) để bồi thường; null = tính theo tổng giá trị các thành phần.</summary>
    public decimal? ReplacementValue { get; set; }
}
