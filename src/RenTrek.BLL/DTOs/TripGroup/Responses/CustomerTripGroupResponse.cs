namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Một TripGroup đã Published mà khách có quyền xem theo booking của họ.
/// </summary>
public sealed class CustomerTripGroupResponse
{
    public Guid TripGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public TripGroupGuideSummaryResponse? Guide { get; set; }
    public IReadOnlyList<CustomerGroupParticipantResponse> Participants { get; set; } = [];
}
