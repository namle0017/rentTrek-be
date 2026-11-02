namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Kết quả ghi nhận điểm danh thực địa hàng loạt tại màn G04 (24.4).
/// </summary>
public sealed class GuideBatchAttendanceResponse
{
    public Guid ActivityId { get; set; }
    public int UpdatedCount { get; set; }
    public int PresentCount { get; set; }
    public int TotalCount { get; set; }
    public string ProgressText { get; set; } = string.Empty;
}
