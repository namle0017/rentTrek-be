namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>
/// Chi tiết sản phẩm cho vận hành: gồm cả sản phẩm / biến thể tạm ngưng và số thiết bị.
/// </summary>
public sealed class AdminProductDetailResponse
{
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<AdminProductVariantResponse> Variants { get; set; } = [];
}
