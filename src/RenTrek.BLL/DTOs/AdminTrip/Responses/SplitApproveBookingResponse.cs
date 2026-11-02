namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Kết quả phê duyệt tách booking đi chung sang nhiều nhóm (9.7).
/// </summary>
public sealed class SplitApproveBookingResponse
{
    public Guid BookingOrderId { get; set; }
    public bool IsSplit { get; set; }
    public string? SplitReason { get; set; }
    public Guid? SplitApprovedByUserId { get; set; }
    public DateTimeOffset? SplitAt { get; set; }
}
