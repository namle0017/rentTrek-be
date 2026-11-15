namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Hủy bớt một số hành khách của booking đã thanh toán; tiền hoàn tính theo số ghế bị hủy.
/// </summary>
public sealed class CancelTripParticipantsRequest : CancelTripBookingRequest
{
    public List<Guid> ParticipantIds { get; set; } = [];
}
