using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP đổi lịch booking chuyến đi.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/trip-bookings")]
public sealed class TripBookingReschedulesController(ITripRescheduleService tripRescheduleService) : ControllerBase
{
    [HttpGet("{orderId:guid}/reschedule-options")]
    [ProducesResponseType(typeof(ApiResponse<RescheduleOptionsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOptionsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await tripRescheduleService.GetOptionsAsync(orderId, cancellationToken);
        return Ok(ApiResponse<RescheduleOptionsResponse>.Ok(result, "Lấy lựa chọn đổi lịch thành công"));
    }

    [HttpPost("{orderId:guid}/reschedule")]
    [ProducesResponseType(typeof(ApiResponse<RescheduleResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RescheduleAsync(
        Guid orderId,
        [FromBody] RescheduleTripBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripRescheduleService.RescheduleAsync(orderId, request, cancellationToken);
        return Ok(ApiResponse<RescheduleResultResponse>.Ok(result, result.Status == "PendingPayment"
            ? "Đã giữ chỗ chuyến mới, vui lòng thanh toán chênh lệch"
            : "Đổi lịch thành công"));
    }
}
