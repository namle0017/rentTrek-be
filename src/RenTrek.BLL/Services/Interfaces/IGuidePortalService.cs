using RenTrek.BLL.DTOs.Guide.Requests;
using RenTrek.BLL.DTOs.Guide.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Định nghĩa dịch vụ cổng Hướng dẫn viên thực địa trên di động (Màn G01 – G06).
/// </summary>
public interface IGuidePortalService
{
    Task<IReadOnlyList<GuideTripCardResponse>> GetMyTripsAsync(
        string? tab = "today",
        CancellationToken cancellationToken = default);

    Task<GuideGroupRosterResponse> GetGroupRosterAsync(
        Guid tripId,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<GuideParticipantDetailResponse> GetParticipantDetailAsync(
        Guid participantId,
        CancellationToken cancellationToken = default);

    Task<GuideBatchAttendanceResponse> SubmitBatchAttendanceAsync(
        Guid tripId,
        GuideBatchAttendanceRequest request,
        CancellationToken cancellationToken = default);

    Task<GuideTripItineraryResponse> GetTripItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<GuideTripProvidedItemsResponse> GetProvidedItemsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
