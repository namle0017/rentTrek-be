namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc danh sách sản phẩm công khai (đang bán, có biến thể đang bán có bảng giá).
/// </summary>
public class ProductFilter
{
    /// <summary>Tìm trong tên hoặc mô tả (chứa, phân biệt hoa thường như DB).</summary>
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public string? OfferingType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStockOnly { get; set; }

    /// <summary>"name", "priceperday" (giá ngày thấp nhất, null cuối) hoặc mặc định CreatedAt.</summary>
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
