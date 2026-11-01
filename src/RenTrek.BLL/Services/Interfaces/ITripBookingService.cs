using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface ITripBookingService
{
    Task<TripBookingResponse> CreateAsync(
        CreateTripBookingRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TripBookingResponse> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParticipantInvitationResponse>> GenerateParticipantInvitationsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<TripBookingResponse> ClaimParticipantInvitationAsync(
        string token,
        CancellationToken cancellationToken = default);
}
