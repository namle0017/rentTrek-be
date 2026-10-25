namespace RenTrek.BLL.DTOs.Product;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product image.
/// </summary>
public class ProductImageResponse
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
