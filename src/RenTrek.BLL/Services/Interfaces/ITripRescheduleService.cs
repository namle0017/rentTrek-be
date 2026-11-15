using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Đổi lịch booking chuyến đi sang chuyến khác cùng tour.</summary>
public interface ITripRescheduleService
{
    Task<RescheduleOptionsResponse> GetOptionsAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<RescheduleResultResponse> RescheduleAsync(
        Guid orderId,
        RescheduleTripBookingRequest request,
        CancellationToken cancellationToken = default);
}
