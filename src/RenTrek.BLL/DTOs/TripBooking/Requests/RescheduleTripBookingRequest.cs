namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>Đổi booking sang chuyến khác cùng tour.</summary>
public sealed class RescheduleTripBookingRequest
{
    public Guid ToTripId { get; set; }
}
