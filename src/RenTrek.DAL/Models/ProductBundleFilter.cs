namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách combo hiển thị cho khách.
/// </summary>
public class ProductBundleFilter
{
    /// <summary>Tìm theo tên combo, tên sản phẩm / biến thể / SKU của thành phần (không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    /// <summary>Giới hạn trong tập combo cho trước (vd. chỉ combo còn hàng); null = không giới hạn.</summary>
    public IReadOnlyCollection<Guid>? RestrictToBundleIds { get; set; }

    /// <summary>name | priceFrom | createdAt (mặc định name).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
