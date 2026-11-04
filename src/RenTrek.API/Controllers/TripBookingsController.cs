using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho trip bookings.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/trip-bookings")]
public sealed class TripBookingsController(
    ITripBookingService tripBookingService,
    ITripGroupViewService tripGroupViewService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TripBookingResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateTripBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.CreateAsync(request, idempotencyKey, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<TripBookingResponse>.Ok(result, "Tạo booking chuyến đi thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TripBookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GetByIdAsync(orderId, cancellationToken);
        return Ok(ApiResponse<TripBookingResponse>.Ok(result, "Lấy booking thành công"));
    }

    [HttpGet("{orderId:guid}/cancellation-preview")]
    [ProducesResponseType(typeof(ApiResponse<CancellationPreviewResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCancellationPreviewAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GetCancellationPreviewAsync(orderId, cancellationToken);
        return Ok(ApiResponse<CancellationPreviewResponse>.Ok(result, "Lấy thông tin hủy booking thành công"));
    }

    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<TripBookingResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> CancelAsync(
        Guid orderId,
        [FromBody] CancelTripBookingRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.CancelAsync(orderId, request, cancellationToken);
        return Ok(ApiResponse<TripBookingResponse>.Ok(result, "Hủy booking thành công"));
    }

    [HttpPut("{orderId:guid}/participants/{participantId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TripBookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateParticipantAsync(
        Guid orderId,
        Guid participantId,
        [FromBody] UpdateBookingParticipantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.UpdateParticipantAsync(orderId, participantId, request, cancellationToken);
        return Ok(ApiResponse<TripBookingResponse>.Ok(result, "Cập nhật thông tin người tham gia thành công"));
    }

    [HttpGet("{orderId:guid}/grouping")]
    [ProducesResponseType(typeof(ApiResponse<CustomerTripGroupingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupingAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripGroupViewService.GetCustomerGroupingAsync(orderId, cancellationToken);
        return Ok(ApiResponse<CustomerTripGroupingResponse>.Ok(result, "Lấy nhóm chuyến đi thành công"));
    }

    [HttpGet("{orderId:guid}/invite-link")]
    [ProducesResponseType(typeof(ApiResponse<InviteLinkResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInviteLinkAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GetInviteLinkAsync(orderId, cancellationToken);
        return Ok(ApiResponse<InviteLinkResponse>.Ok(result, "Lấy link chuyến đi thành công"));
    }

    [HttpPost("{orderId:guid}/invite-link/rotate")]
    [ProducesResponseType(typeof(ApiResponse<InviteLinkResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RotateInviteLinkAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripBookingService.RotateInviteLinkAsync(orderId, cancellationToken);
        return Ok(ApiResponse<InviteLinkResponse>.Ok(result, "Đã đổi link chuyến đi"));
    }

    /// <summary>Trang xem chuyến công khai cần token; dùng POST để token không nằm trên URL.</summary>
    [HttpPost("participant-invitations/preview")]
    [ProducesResponseType(typeof(ApiResponse<InvitationPreviewResponse>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvitationPreviewAsync(
        [FromBody] InvitationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GetInvitationPreviewAsync(request.OrderId, request.Token, cancellationToken);
        return Ok(ApiResponse<InvitationPreviewResponse>.Ok(result, "Lấy thông tin chuyến đi thành công"));
    }
}
