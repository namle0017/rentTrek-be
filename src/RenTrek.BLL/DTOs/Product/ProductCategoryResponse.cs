namespace RenTrek.BLL.DTOs.Product;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product category.
/// </summary>
public class ProductCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
