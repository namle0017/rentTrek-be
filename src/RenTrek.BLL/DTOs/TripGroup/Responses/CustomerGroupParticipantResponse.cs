namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Người tham gia thuộc phạm vi quyền xem của khách trong TripGroup đã chốt.
/// </summary>
public sealed class CustomerGroupParticipantResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
