namespace RenTrek.BLL.DTOs.Product;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ product.
/// </summary>
public class ProductResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public decimal? PricePerDay { get; set; }
    public bool IsRentable => PricePerDay.HasValue;
    public DateTime CreatedAt { get; set; }
}
