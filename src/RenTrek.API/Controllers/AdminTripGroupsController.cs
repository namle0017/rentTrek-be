using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho bảng Kanban chia nhóm chuyến đi và phân công Guide (Màn O12 - Mục 9).
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/trips/{tripId:guid}")]
public sealed class AdminTripGroupsController(
    IAdminTripGroupService adminTripGroupService) : ControllerBase
{
    [HttpGet("groups-board")]
    [ProducesResponseType(typeof(ApiResponse<KanbanGroupsBoardResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupsBoardAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripGroupService.GetGroupsBoardAsync(tripId, cancellationToken);
        return Ok(ApiResponse<KanbanGroupsBoardResponse>.Ok(result, "Lấy bảng phân nhóm thành công"));
    }

    [HttpPost("groups")]
    [ProducesResponseType(typeof(ApiResponse<KanbanGroupColumnResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGroupAsync(
        Guid tripId,
        [FromBody] CreateTripGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripGroupService.CreateGroupAsync(tripId, request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<KanbanGroupColumnResponse>.Ok(result, "Tạo nhóm mới thành công", StatusCodes.Status201Created));
    }

    [HttpPut("groups/{groupId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<KanbanGroupColumnResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGroupAsync(
        Guid tripId,
        Guid groupId,
        [FromBody] UpdateTripGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripGroupService.UpdateGroupAsync(tripId, groupId, request, cancellationToken);
        return Ok(ApiResponse<KanbanGroupColumnResponse>.Ok(result, "Cập nhật thông tin nhóm thành công"));
    }

    [HttpDelete("groups/{groupId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteGroupAsync(
        Guid tripId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        await adminTripGroupService.DeleteGroupAsync(tripId, groupId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đã giải tán nhóm và đưa khách về cột chưa xếp nhóm"));
    }

    [HttpPatch("participants/assign-group")]
    [ProducesResponseType(typeof(ApiResponse<KanbanGroupsBoardResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignParticipantGroupAsync(
        Guid tripId,
        [FromBody] AssignParticipantGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripGroupService.AssignParticipantGroupAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<KanbanGroupsBoardResponse>.Ok(result, "Cập nhật nhóm cho hành khách thành công"));
    }

    [HttpPost("auto-group")]
    [ProducesResponseType(typeof(ApiResponse<AutoGroupResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AutoGroupAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripGroupService.AutoGroupAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AutoGroupResultResponse>.Ok(result, "Tự động chia nhóm thành công"));
    }

    [HttpPost("bookings/{bookingId:guid}/split-approve")]
    [ProducesResponseType(typeof(ApiResponse<SplitApproveBookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveBookingSplitAsync(
        Guid tripId,
        Guid bookingId,
        [FromBody] SplitApproveBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripGroupService.ApproveBookingSplitAsync(
            tripId,
            bookingId,
            request,
            cancellationToken);
        return Ok(ApiResponse<SplitApproveBookingResponse>.Ok(result, "Đã phê duyệt tách nhóm cho đơn đặt chỗ"));
    }
}
