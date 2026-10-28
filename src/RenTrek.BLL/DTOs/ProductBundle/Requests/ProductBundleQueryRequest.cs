using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.ProductBundle.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ product bundle query.
/// SortBy: name | priceFrom | createdAt (mặc định name tăng dần, pageSize 12).
/// </summary>
public class ProductBundleQueryRequest : PagingRequest
{
    public ProductBundleQueryRequest()
    {
        PageSize = 12;
        SortBy = "name";
        SortOrder = "asc";
    }

    public Guid? CategoryId { get; set; }

    /// <summary>Thời điểm nhận dự kiến (UTC). Đi cùng <see cref="To"/>.</summary>
    public DateTime? From { get; set; }

    /// <summary>Thời điểm trả dự kiến (UTC).</summary>
    public DateTime? To { get; set; }

    /// <summary>Chỉ lấy combo còn khả dụng trong from-to (bỏ qua khi không truyền from/to).</summary>
    public bool OnlyAvailable { get; set; }
}
