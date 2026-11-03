using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho trip bookings.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/trip-bookings")]
public sealed class TripBookingsController(ITripBookingService tripBookingService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
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
    public async Task<IActionResult> GetById(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GetByIdAsync(orderId, cancellationToken);
        return Ok(ApiResponse<TripBookingResponse>.Ok(result, "Lấy booking thành công"));
    }

    [HttpGet("{orderId:guid}/cancellation-preview")]
    public async Task<IActionResult> GetCancellationPreview(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GetCancellationPreviewAsync(orderId, cancellationToken);
        return Ok(ApiResponse<CancellationPreviewResponse>.Ok(result, "Lấy thông tin hủy booking thành công"));
    }

    [HttpPost("{orderId:guid}/cancel")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    public async Task<IActionResult> Cancel(
        Guid orderId,
        [FromBody] CancelTripBookingRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.CancelAsync(orderId, request, cancellationToken);
        return Ok(ApiResponse<TripBookingResponse>.Ok(result, "Hủy booking thành công"));
    }

    [HttpPost("{orderId:guid}/participant-invitations")]
    public async Task<IActionResult> GenerateParticipantInvitations(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GenerateParticipantInvitationsAsync(orderId, cancellationToken);
        return Ok(ApiResponse<GroupInvitationResponse>.Ok(
            result,
            "Đã tạo và gửi lời mời participant"));
    }

    /// <summary>Trang link mời: công khai, chỉ trả thông tin chuyến đi và số người còn chờ.</summary>
    [HttpGet("{orderId:guid}/participant-invitations")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvitationPreview(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GetInvitationPreviewAsync(orderId, cancellationToken);
        return Ok(ApiResponse<InvitationPreviewResponse>.Ok(result, "Lấy lời mời thành công"));
    }

    /// <summary>Nhập mã nhóm trước khi đăng nhập: chỉ kiểm tra mã, sai vẫn bị tính lượt. Liên kết thật ở claim.</summary>
    [HttpPost("participant-invitations/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyParticipantInvitationCode(
        [FromBody] ClaimParticipantInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await tripBookingService.VerifyParticipantInvitationCodeAsync(request.OrderId, request.Code, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Mã xác nhận hợp lệ"));
    }

    [HttpPost("participant-invitations/claim")]
    public async Task<IActionResult> ClaimParticipantInvitation(
        [FromBody] ClaimParticipantInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.ClaimParticipantInvitationAsync(request.OrderId, request.Code, cancellationToken);
        return Ok(ApiResponse<TripBookingResponse>.Ok(result, "Liên kết participant thành công"));
    }
}
