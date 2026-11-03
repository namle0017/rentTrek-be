namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Yêu cầu Admin tạo Trip mới ở trạng thái Draft kèm số lượng TripGroup ban đầu.
/// </summary>
public sealed class CreateTripRequest
{
    public Guid TourId { get; set; }
    public Guid? RefundPolicyId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int GroupCount { get; set; }
    public decimal PricePerPerson { get; set; }
    public int MinParticipants { get; set; } = 1;
}
