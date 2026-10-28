namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng combo trong danh sách công khai.
/// </summary>
public class ProductBundleListRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>Giá theo ngày thấp nhất trong các bậc giá chưa xoá.</summary>
    public decimal PriceFrom { get; set; }

    /// <summary>Ảnh chính của thành phần đầu tiên có ảnh; null nếu không có.</summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>Thành phần theo thứ tự tạo (dùng dựng ComponentSummary và tính tồn khả dụng).</summary>
    public List<ProductBundleComponentRow> Components { get; set; } = [];
}
