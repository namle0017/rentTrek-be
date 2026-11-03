using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP quản trị Trip (tạo kèm group, chỉnh sửa khi Draft, chuyển trạng thái Open/ClosedForBooking và sinh từ lịch lặp).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/trips")]
public sealed class TripsController(ITripGroupService tripGroupService) : ControllerBase
{
    [HttpGet("{tripId:guid}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> GetById(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await tripGroupService.GetAdminTripAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AdminTripResponse>.Ok(result, "Lấy thông tin chuyến đi thành công"));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.CreateTripAsync(request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AdminTripResponse>.Ok(result, "Tạo chuyến đi và nhóm ban đầu thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{tripId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Update(
        Guid tripId,
        [FromBody] UpdateTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.UpdateTripAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<AdminTripResponse>.Ok(result, "Cập nhật chuyến đi nháp thành công"));
    }

    [HttpPatch("{tripId:guid}/status")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> UpdateStatus(
        Guid tripId,
        [FromBody] UpdateTripStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.UpdateTripStatusAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<AdminTripResponse>.Ok(result, "Cập nhật trạng thái chuyến đi thành công"));
    }

    [HttpPost("generate-recurring")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GenerateRecurring(
        [FromBody] GenerateRecurringTripsRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await tripGroupService.GenerateRecurringTripsAsync(request ?? new GenerateRecurringTripsRequest(), cancellationToken);
        return Ok(ApiResponse<GenerateRecurringTripsResponse>.Ok(result, "Thực thi sinh chuyến đi từ lịch lặp hoàn tất"));
    }
}
