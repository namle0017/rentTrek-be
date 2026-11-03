using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface ITripBookingService
{
    Task<TripBookingResponse> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<TripBookingResponse> CreateAsync(CreateTripBookingRequest request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<TripBookingResponse> UpdateParticipantAsync(
        Guid orderId,
        Guid participantId,
        UpdateBookingParticipantRequest request,
        CancellationToken cancellationToken = default);
    Task<CancellationPreviewResponse> GetCancellationPreviewAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<TripBookingResponse> CancelAsync(
        Guid orderId,
        CancelTripBookingRequest? request = null,
        CancellationToken cancellationToken = default);
    Task<InviteLinkResponse> GetInviteLinkAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<InviteLinkResponse> RotateInviteLinkAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<InvitationPreviewResponse> GetInvitationPreviewAsync(Guid orderId, string token, CancellationToken cancellationToken = default);
}
