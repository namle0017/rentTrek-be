namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc danh sách đơn thuê cho vận hành.
/// </summary>
public class AdminRentalOrderFilter
{
    public string? Status { get; set; }

    /// <summary>Chỉ đơn đã quá hạn trả mà còn thiết bị đang giao (Issued).</summary>
    public bool OverdueOnly { get; set; }

    /// <summary>Tìm theo mã đơn, tên hoặc email khách (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>Mốc so quá hạn (UTC).</summary>
    public DateTime Now { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
