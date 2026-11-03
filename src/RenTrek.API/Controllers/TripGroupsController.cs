using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP điều phối TripGroup theo mục 8 của API Contract:
/// xem snapshot chia đoàn (Staff/Admin/Guide), gán guide, chuyển/tách booking, tính lại đồ cấp và chốt Published.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/trips/{tripId:guid}")]
public sealed class TripGroupsController(ITripGroupService tripGroupService) : ControllerBase
{
    [HttpGet("grouping")]
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin},{RoleNames.Guide},{RoleNames.TourGuide}")]
    public async Task<IActionResult> GetGroupingSnapshot(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await tripGroupService.GetGroupingSnapshotAsync(tripId, cancellationToken);
        return Ok(ApiResponse<TripGroupingSnapshotResponse>.Ok(result, "Lấy thông tin chia đoàn thành công"));
    }

    [HttpPut("groups/{groupId:guid}/guide")]
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    public async Task<IActionResult> AssignGuide(
        Guid tripId,
        Guid groupId,
        [FromBody] AssignTripGroupGuideRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.AssignGuideAsync(tripId, groupId, request, cancellationToken);
        return Ok(ApiResponse<TripGroupingSnapshotResponse>.Ok(result, "Cập nhật hướng dẫn viên cho group thành công"));
    }

    [HttpPut("bookings/{bookingOrderId:guid}/group")]
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    public async Task<IActionResult> MoveBooking(
        Guid tripId,
        Guid bookingOrderId,
        [FromBody] MoveBookingGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.MoveBookingAsync(tripId, bookingOrderId, request, cancellationToken);
        return Ok(ApiResponse<TripGroupingSnapshotResponse>.Ok(result, "Chuyển booking sang group thành công"));
    }

    [HttpPost("bookings/{bookingOrderId:guid}/split")]
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    public async Task<IActionResult> SplitBooking(
        Guid tripId,
        Guid bookingOrderId,
        [FromBody] SplitTripBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.SplitBookingAsync(tripId, bookingOrderId, request, cancellationToken);
        return Ok(ApiResponse<TripGroupingSnapshotResponse>.Ok(result, "Tách booking thành công"));
    }

    [HttpPost("grouping/recalculate-provided-items")]
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    public async Task<IActionResult> RecalculateProvidedItems(
        Guid tripId,
        [FromBody] PublishTripGroupingRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.RecalculateProvidedItemsAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<RecalculateTripProvidedItemsResponse>.Ok(result, "Tính lại nhu cầu đồ cấp chuyến thành công"));
    }

    [HttpPost("grouping/publish")]
    [Authorize(Roles = $"{RoleNames.Staff},{RoleNames.Admin}")]
    public async Task<IActionResult> Publish(
        Guid tripId,
        [FromBody] PublishTripGroupingRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.PublishGroupsAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<TripGroupingSnapshotResponse>.Ok(result, "Chốt danh sách group thành công"));
    }
}
