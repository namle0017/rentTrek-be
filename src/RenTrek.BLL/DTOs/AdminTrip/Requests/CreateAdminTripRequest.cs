namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu tạo chuyến đi thủ công (8.2).
/// </summary>
public sealed class CreateAdminTripRequest
{
    public Guid TourId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int MinParticipants { get; set; }
    public decimal PricePerPerson { get; set; }
    public Guid RefundPolicyId { get; set; }
    public Guid? ManagedByUserId { get; set; }
}
