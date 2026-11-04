namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu sinh hàng loạt chuyến đi từ quy tắc lặp (7.6).
/// </summary>
public sealed class GenerateRecurrenceTripsRequest
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public Guid? RefundPolicyId { get; set; }
    public Guid? ManagedByUserId { get; set; }
    public int? MinParticipants { get; set; }
}
