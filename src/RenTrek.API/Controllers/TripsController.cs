using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.DTOs.TripSuggestion.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP quản trị Trip (tạo kèm group, chỉnh sửa khi Draft, chuyển trạng thái Open/ClosedForBooking và sinh từ lịch lặp).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/trips")]
public sealed class TripsController(
    ITripService tripService,
    ITripRecurrenceService tripRecurrenceService,
    IProductBundleService productBundleService) : ControllerBase
{
    [HttpGet("{tripId:guid}/suggested-bundles")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TripSuggestedBundlesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSuggestedBundlesAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await productBundleService.GetSuggestedForTripAsync(tripId, cancellationToken);
        return Ok(ApiResponse<TripSuggestedBundlesResponse>.Ok(result, "Lấy combo gợi ý cho chuyến đi thành công"));
    }

    [HttpGet("{tripId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> GetByIdAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await tripService.GetAdminTripAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AdminTripResponse>.Ok(result, "Lấy thông tin chuyến đi thành công"));
    }

    [HttpGet("{tripId:guid}/refund-policy")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TripRefundPolicyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefundPolicyAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await tripService.GetTripRefundPolicyAsync(tripId, cancellationToken);
        return Ok(ApiResponse<TripRefundPolicyResponse>.Ok(result, "Lấy chính sách hoàn tiền của chuyến đi thành công"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminTripResponse>), StatusCodes.Status201Created)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripService.CreateTripAsync(request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AdminTripResponse>.Ok(result, "Tạo chuyến đi và nhóm ban đầu thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{tripId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateAsync(
        Guid tripId,
        [FromBody] UpdateTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripService.UpdateTripAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<AdminTripResponse>.Ok(result, "Cập nhật chuyến đi nháp thành công"));
    }

    [HttpPatch("{tripId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> UpdateStatusAsync(
        Guid tripId,
        [FromBody] UpdateTripStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripService.UpdateTripStatusAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<AdminTripResponse>.Ok(result, "Cập nhật trạng thái chuyến đi thành công"));
    }

    [HttpPost("generate-recurring")]
    [ProducesResponseType(typeof(ApiResponse<GenerateRecurringTripsResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GenerateRecurringAsync(
        [FromBody] GenerateRecurringTripsRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await tripRecurrenceService.GenerateRecurringTripsAsync(request ?? new GenerateRecurringTripsRequest(), cancellationToken);
        return Ok(ApiResponse<GenerateRecurringTripsResponse>.Ok(result, "Thực thi sinh chuyến đi từ lịch lặp hoàn tất"));
    }
}
