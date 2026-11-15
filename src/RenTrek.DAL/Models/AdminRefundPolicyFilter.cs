namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách chính sách hoàn tiền trang quản trị (chỉ chính sách chưa xoá).
/// </summary>
public class AdminRefundPolicyFilter
{
    /// <summary>Tìm theo tên (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>Tour | Rental.</summary>
    public string? PolicyType { get; set; }

    /// <summary>Trạng thái hiển thị: Draft | Scheduled | Active | Expired | Archived.</summary>
    public string? DisplayStatus { get; set; }

    /// <summary>true: chỉ phiên bản mới nhất (version lớn nhất chưa xoá) của mỗi họ chính sách.</summary>
    public bool LatestOnly { get; set; } = true;

    /// <summary>Thời điểm dùng để suy ra trạng thái hiển thị.</summary>
    public DateTime NowUtc { get; set; }

    /// <summary>CreatedAt | Name | EffectiveFrom | Version (mặc định CreatedAt).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
