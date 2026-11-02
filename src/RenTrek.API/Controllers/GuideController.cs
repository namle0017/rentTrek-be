using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Guide.Requests;
using RenTrek.BLL.DTOs.Guide.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho khu vực Hướng dẫn viên thực địa trên di động (Màn G01–G06 - Mục 24).
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Guide},{RoleNames.Admin}")]
[Route("api/v1/guide")]
public sealed class GuideController(IGuidePortalService guidePortalService) : ControllerBase
{
    [HttpGet("trips")]
    public async Task<IActionResult> GetMyTrips(
        [FromQuery] string? tab,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetMyTripsAsync(tab, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GuideTripCardResponse>>.Ok(result, "Thành công"));
    }

    [HttpGet("trips/{tripId:guid}/group")]
    public async Task<IActionResult> GetGroupRoster(
        Guid tripId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetGroupRosterAsync(tripId, search, cancellationToken);
        return Ok(ApiResponse<GuideGroupRosterResponse>.Ok(result, "Thành công"));
    }

    [HttpGet("participants/{participantId:guid}")]
    public async Task<IActionResult> GetParticipantDetail(
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetParticipantDetailAsync(participantId, cancellationToken);
        return Ok(ApiResponse<GuideParticipantDetailResponse>.Ok(result, "Thành công"));
    }

    [HttpPost("trips/{tripId:guid}/attendances/batch")]
    public async Task<IActionResult> SubmitBatchAttendance(
        Guid tripId,
        [FromBody] GuideBatchAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.SubmitBatchAttendanceAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<GuideBatchAttendanceResponse>.Ok(result, "Ghi nhận điểm danh thành công"));
    }

    [HttpGet("trips/{tripId:guid}/itinerary")]
    public async Task<IActionResult> GetTripItinerary(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetTripItineraryAsync(tripId, cancellationToken);
        return Ok(ApiResponse<GuideTripItineraryResponse>.Ok(result, "Thành công"));
    }

    [HttpGet("trips/{tripId:guid}/provided-items")]
    public async Task<IActionResult> GetProvidedItems(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetProvidedItemsAsync(tripId, cancellationToken);
        return Ok(ApiResponse<GuideTripProvidedItemsResponse>.Ok(result, "Thành công"));
    }
}
