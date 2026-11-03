namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Thông tin người tham gia trong một khối booking của TripGroup.
/// </summary>
public sealed class TripGroupParticipantItemResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
