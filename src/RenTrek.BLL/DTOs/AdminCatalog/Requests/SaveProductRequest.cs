namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body tạo / sửa sản phẩm cha (§13.2, §13.4). Status: Active | Inactive | Draft (Draft lưu như Inactive).</summary>
public sealed class SaveProductRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "Draft";
}
