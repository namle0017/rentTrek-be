namespace RenTrek.BLL.DTOs.ProductBundle.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào khoảng thuê tuỳ chọn (from-to) khi xem chi tiết combo.
/// </summary>
public class RentalPeriodQueryRequest
{
    /// <summary>Thời điểm nhận dự kiến (UTC).</summary>
    public DateTime? From { get; set; }

    /// <summary>Thời điểm trả dự kiến (UTC).</summary>
    public DateTime? To { get; set; }
}
