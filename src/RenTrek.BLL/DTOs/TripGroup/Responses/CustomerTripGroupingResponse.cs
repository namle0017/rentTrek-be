namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Kết quả tra cứu phân nhóm của một booking dành cho khách hàng (Pending trước khi chốt, Published sau khi chốt).
/// </summary>
public sealed class CustomerTripGroupingResponse
{
    public Guid TripId { get; set; }
    public string GroupingStatus { get; set; } = string.Empty;
    public IReadOnlyList<CustomerTripGroupResponse> Groups { get; set; } = [];
}
