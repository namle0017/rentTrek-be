using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Warehouse.Interfaces;

namespace RenTrek.API.Controllers.Admin.Warehouse;

/// <summary>
/// Cung cấp các endpoint HTTP bàn giao và thu hồi thiết bị đơn thuê qua máy quét Barcode / QR (Mục 15.4 - 15.10 Contract, Màn O24, O25).
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
public class RentalHandoverController : ControllerBase
{
    private readonly IFulfillmentService _fulfillmentService;
    private readonly ICurrentUserService _currentUserService;

    public RentalHandoverController(
        IFulfillmentService fulfillmentService,
        ICurrentUserService currentUserService)
    {
        _fulfillmentService = fulfillmentService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// 15.4. Danh sách thiết bị cần bàn giao (Handover Checklist) (Màn O24).
    /// </summary>
    [HttpGet("api/v1/admin/rental-orders/{orderCode}/handover-checklist")]
    [ProducesResponseType(typeof(ApiResponse<HandoverChecklistResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHandoverChecklistAsync(
        [FromRoute] string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _fulfillmentService.GetHandoverChecklistAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<HandoverChecklistResponse>.Ok(result, "Lấy danh sách bàn giao thành công"));
    }

    /// <summary>
    /// 15.5. Quét barcode kiểm tra & bàn giao từng thiết bị (Màn O24).
    /// </summary>
    [HttpPost("api/v1/admin/rental-orders/{orderCode}/handover/scan")]
    [ProducesResponseType(typeof(ApiResponse<HandoverScanResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanHandoverAssetAsync(
        [FromRoute] string orderCode,
        [FromBody] HandoverScanRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;
        var result = await _fulfillmentService.ScanHandoverAssetAsync(orderCode, request, userId, cancellationToken);
        return Ok(ApiResponse<HandoverScanResultResponse>.Ok(result, "Quét mã bàn giao hợp lệ"));
    }

    /// <summary>
    /// 15.6. Hoàn tất bàn giao thiết bị cho khách (Màn O24).
    /// </summary>
    [HttpPost("api/v1/admin/rental-orders/{orderCode}/handover/complete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteHandoverAsync(
        [FromRoute] string orderCode,
        [FromBody] CompleteHandoverRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;
        var result = await _fulfillmentService.CompleteHandoverAsync(orderCode, request, userId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Hoàn tất bàn giao thiết bị thành công"));
    }

    /// <summary>
    /// 15.7. Danh sách thiết bị cần thu hồi (Return Checklist) (Màn O25).
    /// </summary>
    [HttpGet("api/v1/admin/rental-orders/{orderCode}/return-checklist")]
    [ProducesResponseType(typeof(ApiResponse<ReturnChecklistResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReturnChecklistAsync(
        [FromRoute] string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _fulfillmentService.GetReturnChecklistAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<ReturnChecklistResponse>.Ok(result, "Lấy danh sách thu hồi thành công"));
    }

    /// <summary>
    /// 15.8. Quét barcode nhận lại & kiểm tra tình trạng thiết bị (Màn O25).
    /// </summary>
    [HttpPost("api/v1/admin/rental-orders/{orderCode}/return/scan")]
    [ProducesResponseType(typeof(ApiResponse<ReturnScanResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanReturnAssetAsync(
        [FromRoute] string orderCode,
        [FromBody] ReturnScanRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;
        var result = await _fulfillmentService.ScanReturnAssetAsync(orderCode, request, userId, cancellationToken);
        return Ok(ApiResponse<ReturnScanResultResponse>.Ok(result, "Quét mã nhận trả đồ thành công"));
    }

    /// <summary>
    /// 15.9. Báo mất thiết bị (Report Lost) (Màn O25).
    /// </summary>
    [HttpPost("api/v1/admin/rental-orders/{orderCode}/return/report-lost")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportLostAssetAsync(
        [FromRoute] string orderCode,
        [FromBody] ReportLostAssetRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;
        var result = await _fulfillmentService.ReportLostAssetAsync(orderCode, request, userId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Ghi nhận báo mất thiết bị thành công"));
    }

    /// <summary>
    /// 15.10. Hoàn tất thu hồi thiết bị sau thuê (Màn O25).
    /// </summary>
    [HttpPost("api/v1/admin/rental-orders/{orderCode}/return/complete")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteReturnAsync(
        [FromRoute] string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _fulfillmentService.CompleteReturnAsync(orderCode, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(result, "Hoàn tất thu hồi toàn bộ thiết bị"));
    }
}
