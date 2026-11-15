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
[Authorize(Roles = RoleNames.Guide)]
[Route("api/v1/guide")]
public sealed class GuideController(IGuidePortalService guidePortalService) : ControllerBase
{
    [HttpGet("trips")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GuideTripCardResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTripsAsync(
        [FromQuery] string? tab,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetMyTripsAsync(tab, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GuideTripCardResponse>>.Ok(result, "Lấy danh sách chuyến được phân công thành công"));
    }

    [HttpGet("trips/{tripId:guid}/group")]
    [ProducesResponseType(typeof(ApiResponse<GuideGroupRosterResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupRosterAsync(
        Guid tripId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetGroupRosterAsync(tripId, search, cancellationToken);
        return Ok(ApiResponse<GuideGroupRosterResponse>.Ok(result, "Lấy danh sách thành viên nhóm thành công"));
    }

    [HttpGet("participants/{participantId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GuideParticipantDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParticipantDetailAsync(
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetParticipantDetailAsync(participantId, cancellationToken);
        return Ok(ApiResponse<GuideParticipantDetailResponse>.Ok(result, "Lấy thông tin thành viên thành công"));
    }

    [HttpPost("trips/{tripId:guid}/attendances/batch")]
    [ProducesResponseType(typeof(ApiResponse<GuideBatchAttendanceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitBatchAttendanceAsync(
        Guid tripId,
        [FromBody] GuideBatchAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.SubmitBatchAttendanceAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<GuideBatchAttendanceResponse>.Ok(result, "Ghi nhận điểm danh thành công"));
    }

    [HttpGet("trips/{tripId:guid}/itinerary")]
    [ProducesResponseType(typeof(ApiResponse<GuideTripItineraryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTripItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetTripItineraryAsync(tripId, cancellationToken);
        return Ok(ApiResponse<GuideTripItineraryResponse>.Ok(result, "Lấy lịch trình chuyến thành công"));
    }

    [HttpGet("trips/{tripId:guid}/provided-items")]
    [ProducesResponseType(typeof(ApiResponse<GuideTripProvidedItemsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvidedItemsAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await guidePortalService.GetProvidedItemsAsync(tripId, cancellationToken);
        return Ok(ApiResponse<GuideTripProvidedItemsResponse>.Ok(result, "Lấy danh sách đồ cấp phát thành công"));
    }
}
