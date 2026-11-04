using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint quản lý đồ cấp chuyến đi và phân bổ thiết bị vật lý (Mục 10 Contract, Màn O13).
/// </summary>
[ApiController]
[Route("api/v1/admin/trips/{tripId:guid}")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public class TripProvidedItemsController : ControllerBase
{
    private readonly ITripProvidedItemService _providedItemService;
    private readonly ICurrentUserService _currentUserService;

    public TripProvidedItemsController(
        ITripProvidedItemService providedItemService,
        ICurrentUserService currentUserService)
    {
        _providedItemService = providedItemService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// 10.1. Danh sách đồ cấp cần chuẩn bị cho chuyến đi (Màn O13).
    /// </summary>
    [HttpGet("provided-items")]
    [ProducesResponseType(typeof(ApiResponse<TripProvidedItemListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProvidedItemsByTripIdAsync(
        [FromRoute] Guid tripId,
        CancellationToken cancellationToken)
    {
        var result = await _providedItemService.GetProvidedItemsByTripIdAsync(tripId, cancellationToken);
        return Ok(ApiResponse<TripProvidedItemListResponse>.Ok(result, "Lấy danh sách đồ cấp chuyến đi thành công"));
    }

    /// <summary>
    /// 10.2. Tra cứu thiết bị trống trong kho theo khung ngày của chuyến (Màn O13).
    /// </summary>
    [HttpGet("provided-items/{itemId:guid}/available-assets")]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableAssetResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableAssetsForItemAsync(
        [FromRoute] Guid tripId,
        [FromRoute] Guid itemId,
        CancellationToken cancellationToken)
    {
        var result = await _providedItemService.GetAvailableAssetsForItemAsync(tripId, itemId, cancellationToken);
        return Ok(ApiResponse<List<AvailableAssetResponse>>.Ok(result, "Tra cứu thiết bị khả dụng thành công"));
    }

    /// <summary>
    /// 10.3. Gán thiết bị từ kho cho chuyến (Màn O13).
    /// </summary>
    [HttpPost("provided-items/{itemId:guid}/assignments")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignAssetsToTripItemAsync(
        [FromRoute] Guid tripId,
        [FromRoute] Guid itemId,
        [FromBody] AssignTripAssetsRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.RequiredUserId;
        var result = await _providedItemService.AssignAssetsToTripItemAsync(tripId, itemId, request, currentUserId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Gán thiết bị cho chuyến đi thành công"));
    }

    /// <summary>
    /// 10.4. Hủy gán thiết bị khỏi chuyến đi (Màn O13).
    /// </summary>
    [HttpDelete("assignments/{assignmentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignAssetFromTripAsync(
        [FromRoute] Guid tripId,
        [FromRoute] Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var result = await _providedItemService.UnassignAssetFromTripAsync(tripId, assignmentId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Hủy gán thiết bị thành công"));
    }

    /// <summary>
    /// 10.5. Xuất kho bàn giao thiết bị cho đoàn (Issue Gear) (Màn O13).
    /// </summary>
    [HttpPost("provided-items/issue")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueTripGearAsync(
        [FromRoute] Guid tripId,
        [FromBody] IssueTripGearRequest request,
        CancellationToken cancellationToken)
    {
        var count = await _providedItemService.IssueTripGearAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count, $"Đã xuất kho bàn giao {count} thiết bị cho đoàn"));
    }

    /// <summary>
    /// 10.6. Thu hồi thiết bị sau chuyến (Return Gear) (Màn O13).
    /// </summary>
    [HttpPost("provided-items/return")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReturnTripGearAsync(
        [FromRoute] Guid tripId,
        [FromBody] ReturnTripGearRequest request,
        CancellationToken cancellationToken)
    {
        var count = await _providedItemService.ReturnTripGearAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count, $"Đã thu hồi thành công {count} thiết bị sau chuyến"));
    }
}
