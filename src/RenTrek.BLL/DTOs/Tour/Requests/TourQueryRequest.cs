using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Tour.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ tour query.
/// </summary>
public class TourQueryRequest : PagingRequest
{
    public Guid? CategoryId { get; set; }
    public Guid? LocationId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? DifficultyLevel { get; set; }
    public int? DurationDays { get; set; }

    /// <summary>
    /// Lọc theo tab trạng thái: "Active" (Đang mở) hoặc "Expired" (Hết hạn). Để trống lấy tất cả.
    /// </summary>
    public string? Status { get; set; }
}
