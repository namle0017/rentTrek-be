namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu cập nhật thông tin chuyến đi kèm kiểm tra đồng thời rowVersion (8.4).
/// </summary>
public sealed class UpdateAdminTripRequest
{
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSlots { get; set; }
    public int MinParticipants { get; set; }
    public decimal PricePerPerson { get; set; }
    public Guid? RefundPolicyId { get; set; }
    public Guid? ManagedByUserId { get; set; }
    public int RowVersion { get; set; }
}
