using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("{orderId:guid}/participant-invitations")]
    public async Task<IActionResult> GenerateParticipantInvitations(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.GenerateParticipantInvitationsAsync(orderId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParticipantInvitationResponse>>.Ok(
            result,
            "Tạo lời mời participant thành công"));
    }

    [HttpPost("participant-invitations/claim")]
    public async Task<IActionResult> ClaimParticipantInvitation(
        [FromBody] ClaimParticipantInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripBookingService.ClaimParticipantInvitationAsync(request.Token, cancellationToken);
        return Ok(ApiResponse<TripBookingResponse>.Ok(result, "Liên kết participant thành công"));
    }
}
