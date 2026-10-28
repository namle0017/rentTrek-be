namespace RenTrek.DAL.Models;

/// <summary>
/// Thông tin biến thể thiết bị phục vụ dựng dòng thuê (tên hiển thị, SKU, còn kinh doanh hay không).
/// </summary>
public class RentalVariantInfo
{
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;

    /// <summary>True khi biến thể và sản phẩm đều Active và chưa bị xoá.</summary>
    public bool IsActive { get; set; }
}
