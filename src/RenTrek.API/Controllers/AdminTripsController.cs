using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP quản lý chuyến đi và điều hành khởi hành cho Admin/Staff (Màn O09, O10, O11 - Mục 8).
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/trips")]
public sealed class AdminTripsController(IAdminTripService adminTripService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminTripItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPagedAsync(
        [FromQuery] AdminTripQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.GetPagedAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminTripItemResponse>>.Ok(result, "Lấy danh sách chuyến đi thành công"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminTripDetailResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateAdminTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.CreateAsync(request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AdminTripDetailResponse>.Ok(result, "Tạo chuyến đi thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{tripId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.GetByIdAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AdminTripDetailResponse>.Ok(result, "Lấy chi tiết chuyến đi thành công"));
    }

    [HttpPut("{tripId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        Guid tripId,
        [FromBody] UpdateAdminTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.UpdateAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<AdminTripDetailResponse>.Ok(result, "Cập nhật thông tin chuyến đi thành công"));
    }

    [HttpGet("{tripId:guid}/readiness-checklist")]
    [ProducesResponseType(typeof(ApiResponse<TripReadinessChecklistResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReadinessChecklistAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.GetReadinessChecklistAsync(tripId, cancellationToken);
        return Ok(ApiResponse<TripReadinessChecklistResponse>.Ok(result, "Lấy checklist sẵn sàng thành công"));
    }

    [HttpPost("{tripId:guid}/open-booking")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenBookingAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.OpenBookingAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AdminTripDetailResponse>.Ok(result, "Đã mở nhận khách cho chuyến đi"));
    }

    [HttpPost("{tripId:guid}/close-booking")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseBookingAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.CloseBookingAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AdminTripDetailResponse>.Ok(result, "Đã chốt danh sách nhận khách"));
    }

    [HttpPost("{tripId:guid}/start")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartTripAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.StartTripAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AdminTripDetailResponse>.Ok(result, "Chuyến đi đã bắt đầu xuất phát"));
    }

    [HttpPost("{tripId:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<AdminTripDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteTripAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.CompleteTripAsync(tripId, cancellationToken);
        return Ok(ApiResponse<AdminTripDetailResponse>.Ok(result, "Chuyến đi đã hoàn tất"));
    }

    [HttpPost("{tripId:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<CancelAdminTripResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CancelTripAsync(
        Guid tripId,
        [FromBody] CancelAdminTripRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.CancelTripAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<CancelAdminTripResponse>.Ok(result, "Hủy chuyến thành công"));
    }

    [HttpGet("{tripId:guid}/bookings")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminTripBookingItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTripBookingsAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await adminTripService.GetTripBookingsAsync(tripId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminTripBookingItemResponse>>.Ok(result, "Lấy danh sách booking của chuyến thành công"));
    }

    [HttpGet("{tripId:guid}/manifest")]
    [ProducesResponseType(typeof(ApiResponse<TripManifestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManifestAsync(
        Guid tripId,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var (fileBytes, fileName) = await adminTripService.ExportManifestCsvAsync(tripId, cancellationToken);
            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        var result = await adminTripService.GetManifestAsync(tripId, cancellationToken);
        return Ok(ApiResponse<TripManifestResponse>.Ok(result, "Lấy danh sách hành khách thành công"));
    }
}
