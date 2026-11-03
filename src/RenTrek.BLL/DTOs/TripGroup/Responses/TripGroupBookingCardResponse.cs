namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Một khối booking nằm trong một TripGroup hoặc trong mục Chưa xếp.
/// </summary>
public sealed class TripGroupBookingCardResponse
{
    public Guid BookingOrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public bool IsSplit { get; set; }
    public IReadOnlyList<TripGroupParticipantItemResponse> Participants { get; set; } = [];
}
