namespace RenTrek.BLL.DTOs.Guide.Requests;

/// <summary>
/// Yêu cầu điểm danh thực địa hàng loạt trên di động hỗ trợ Idempotency (24.4).
/// </summary>
public sealed class GuideBatchAttendanceRequest
{
    public Guid ActivityId { get; set; }
    public string? ClientBatchId { get; set; }
    public bool MarkAllPresent { get; set; }
    public IReadOnlyList<GuideAttendanceRecordItemRequest> Attendances { get; set; } = [];
}
