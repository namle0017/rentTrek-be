using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Bộ lọc danh sách chính sách hoàn tiền. SortBy: createdAt | name | effectiveFrom | version.
/// </summary>
public sealed class RefundPolicyQueryRequest : PagingRequest
{
    /// <summary>Tour | Rental.</summary>
    public string? PolicyType { get; set; }

    /// <summary>Trạng thái hiển thị: Draft | Scheduled | Active | Expired | Archived.</summary>
    public string? Status { get; set; }

    /// <summary>true (mặc định): mỗi họ chính sách chỉ hiện phiên bản mới nhất.</summary>
    public bool LatestOnly { get; set; } = true;
}
