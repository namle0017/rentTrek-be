namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Một mốc trong dòng thời gian của đơn thuê.
/// </summary>
public class RentalOrderTimelineEventResponse
{
    public string Event { get; set; } = string.Empty;

    public DateTime At { get; set; }
}
