using RenTrek.DAL.Entities.Enums;

namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc danh sách sản phẩm cho vận hành (sản phẩm và danh mục chưa xoá).
/// </summary>
public class AdminProductFilter
{
    /// <summary>Tìm theo tên sản phẩm hoặc SKU biến thể chưa xoá (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public CatalogStatus? Status { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
