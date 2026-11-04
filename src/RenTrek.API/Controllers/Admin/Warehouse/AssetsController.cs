using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.BLL.Services.Warehouse.Interfaces;

namespace RenTrek.API.Controllers.Admin.Warehouse;

/// <summary>
/// Cung cấp các endpoint HTTP quản lý kho thiết bị vật lý và lịch phân bổ Gantt (Mục 14 Contract, Màn O20, O21).
/// </summary>
[ApiController]
[Route("api/v1/admin/assets")]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assetService;

    public AssetsController(IAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// 14.1. Lấy danh sách thiết bị vật lý có gắn mã vạch QR trong kho (Màn O20).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AssetResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPagedAssetsAsync(
        [FromQuery] AssetQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assetService.GetPagedAssetsAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AssetResponse>>.Ok(result, "Lấy danh sách thiết bị thành công"));
    }

    /// <summary>
    /// 14.2. Thêm hàng loạt thiết bị vật lý vào kho theo tiền tố mã tự sinh (Màn O20).
    /// </summary>
    [HttpPost("bulk-create")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AssetResponse>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkCreateAssetsAsync(
        [FromBody] BulkCreateAssetsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assetService.BulkCreateAssetsAsync(request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<IReadOnlyList<AssetResponse>>.Ok(result, "Tạo lô thiết bị thành công", StatusCodes.Status201Created));
    }

    /// <summary>
    /// 14.6. Lấy dữ liệu biểu đồ Gantt phân bổ thiết bị theo dòng thời gian (Màn O21).
    /// </summary>
    [HttpGet("schedule-gantt")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<AssetGanttResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGanttScheduleAsync(
        [FromQuery] AssetGanttQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assetService.GetGanttScheduleAsync(request, cancellationToken);
        return Ok(ApiResponse<AssetGanttResponse>.Ok(result, "Lấy lịch phân bổ thiết bị thành công"));
    }

    /// <summary>
    /// 14.3. Lấy chi tiết thiết bị & trạng thái sử dụng hiện tại (Màn O20 Drawer).
    /// </summary>
    [HttpGet("{assetCode}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<AssetDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssetByCodeAsync(
        string assetCode,
        CancellationToken cancellationToken)
    {
        var result = await _assetService.GetAssetByCodeAsync(assetCode, cancellationToken);
        return Ok(ApiResponse<AssetDetailResponse>.Ok(result, "Lấy chi tiết thiết bị thành công"));
    }

    /// <summary>
    /// 14.4. Đổi tình trạng kỹ thuật thiết bị (Sẵn sàng, Bảo trì, Hỏng, Mất, Ngừng dùng).
    /// </summary>
    [HttpPatch("{assetCode}/status")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<AssetDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAssetStatusAsync(
        string assetCode,
        [FromBody] UpdateAssetStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assetService.UpdateAssetStatusAsync(assetCode, request, cancellationToken);
        return Ok(ApiResponse<AssetDetailResponse>.Ok(result, "Cập nhật tình trạng thiết bị thành công"));
    }

    /// <summary>
    /// 14.5. Lịch sử phân bổ của thiết bị (đơn thuê, chuyến tour, quà tặng).
    /// </summary>
    [HttpGet("{assetCode}/history")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AssetAssignmentHistoryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssetHistoryAsync(
        string assetCode,
        CancellationToken cancellationToken)
    {
        var result = await _assetService.GetAssetHistoryAsync(assetCode, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AssetAssignmentHistoryResponse>>.Ok(result, "Lấy lịch sử phân bổ thành công"));
    }
}
