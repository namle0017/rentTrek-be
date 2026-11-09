namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Yêu cầu Admin cập nhật Trip và đồng bộ số lượng TripGroup khi Trip còn ở trạng thái Draft.
/// </summary>
public sealed class UpdateTripRequest
{
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int GroupCount { get; set; }
    public decimal PricePerPerson { get; set; }
    public int MinParticipants { get; set; } = 1;
}
