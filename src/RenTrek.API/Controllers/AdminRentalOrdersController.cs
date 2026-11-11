using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalOps.Requests;
using RenTrek.BLL.DTOs.RentalOps.Responses;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Vận hành đơn thuê (Staff/Admin): giao đồ, nhận trả từng thiết bị, phí trễ / hỏng / mất, đóng đơn.
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/rental-orders")]
[Produces("application/json")]
public sealed class AdminRentalOrdersController(IRentalFulfillmentService fulfillment) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminRentalOrderListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromQuery] AdminRentalOrderQueryRequest query, CancellationToken cancellationToken)
        => Ok(ApiResponse<PagedResponse<AdminRentalOrderListItemResponse>>.Ok(
            await fulfillment.GetListAsync(query, cancellationToken), "Lấy danh sách đơn thuê thành công"));

    /// <summary>Danh sách từng thiết bị cần giao trong ngày giờ Việt Nam (JSON hoặc CSV khi format=csv).</summary>
    [HttpGet("handover-list")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHandoverListAsync(
        [FromQuery(Name = "date")] DateOnly? targetDate,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var (fileBytes, fileName) = await fulfillment.ExportHandoverListCsvAsync(targetDate, cancellationToken);
            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        var result = await fulfillment.GetHandoverListAsync(targetDate, cancellationToken);
        return Ok(ApiResponse<AdminRentalScheduleResponse>.Ok(result, "Lấy danh sách giao đồ theo ngày thành công"));
    }

    /// <summary>Danh sách từng thiết bị cần nhận lại trong ngày giờ Việt Nam (JSON hoặc CSV khi format=csv).</summary>
    [HttpGet("return-list")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalScheduleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReturnListAsync(
        [FromQuery(Name = "date")] DateOnly? targetDate,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var (fileBytes, fileName) = await fulfillment.ExportReturnListCsvAsync(targetDate, cancellationToken);
            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        var result = await fulfillment.GetReturnListAsync(targetDate, cancellationToken);
        return Ok(ApiResponse<AdminRentalScheduleResponse>.Ok(result, "Lấy danh sách nhận lại đồ theo ngày thành công"));
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid orderId, CancellationToken cancellationToken)
        => Detail(await fulfillment.GetDetailAsync(orderId, cancellationToken), "Lấy chi tiết đơn thuê thành công");

    /// <summary>Lịch sử điều chỉnh (gia hạn) của đơn thuê (FR-R09).</summary>
    [HttpGet("{orderId:guid}/adjustments")]
    [ProducesResponseType(typeof(ApiResponse<List<RentalOrderAdjustmentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdjustmentsAsync([FromRoute] Guid orderId, CancellationToken cancellationToken)
        => Ok(ApiResponse<List<RentalOrderAdjustmentResponse>>.Ok(
            await fulfillment.GetAdjustmentsAsync(orderId, cancellationToken), "Lấy lịch sử điều chỉnh đơn thuê thành công"));

    /// <summary>Nhận phụ trách đơn (FR-R05). Admin được nhận lại đơn đã có người phụ trách.</summary>
    [HttpPost("{orderId:guid}/claim")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClaimAsync([FromRoute] Guid orderId, CancellationToken cancellationToken)
        => Detail(await fulfillment.ClaimAsync(orderId, cancellationToken), "Đã nhận phụ trách đơn thuê");

    /// <summary>Quét / nhập mã một thiết bị để giao (FR-R06).</summary>
    [HttpPost("{orderId:guid}/handover/scan")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScanHandoverAsync(
        [FromRoute] Guid orderId,
        [FromBody] ScanHandoverRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.ScanHandoverAsync(orderId, request, cancellationToken), "Đã giao thiết bị");

    [HttpPost("{orderId:guid}/handover")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> HandoverAsync(
        [FromRoute] Guid orderId,
        [FromBody] HandoverRentalRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.HandoverAsync(orderId, request, cancellationToken), "Đã giao thiết bị cho khách");

    [HttpPost("{orderId:guid}/returns")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnAsync(
        [FromRoute] Guid orderId,
        [FromBody] ReturnRentalAssetsRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.ReturnAsync(orderId, request, cancellationToken), "Đã ghi nhận trả thiết bị");

    [HttpPost("{orderId:guid}/charges")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateChargeAsync(
        [FromRoute] Guid orderId,
        [FromBody] CreateRentalChargeRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.CreateChargeAsync(orderId, request, cancellationToken), "Đã lập khoản phí");

    [HttpPost("{orderId:guid}/charges/{chargeId:guid}/assess")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssessChargeAsync([FromRoute] Guid orderId, [FromRoute] Guid chargeId, CancellationToken cancellationToken)
        => Detail(await fulfillment.AssessChargeAsync(orderId, chargeId, cancellationToken), "Đã xác nhận khoản phí");

    /// <summary>Hủy phí. Phí Draft: Staff/Admin; phí Assessed: chỉ Admin.</summary>
    [HttpPost("{orderId:guid}/charges/{chargeId:guid}/void")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VoidChargeAsync(
        [FromRoute] Guid orderId,
        [FromRoute] Guid chargeId,
        [FromBody] VoidRentalChargeRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.VoidChargeAsync(orderId, chargeId, request, cancellationToken), "Đã hủy khoản phí");

    [HttpPost("{orderId:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteAsync([FromRoute] Guid orderId, CancellationToken cancellationToken)
        => Detail(await fulfillment.CompleteAsync(orderId, cancellationToken), "Đã đóng đơn thuê");

    private OkObjectResult Detail(AdminRentalOrderDetailResponse detail, string message)
        => Ok(ApiResponse<AdminRentalOrderDetailResponse>.Ok(detail, message));
}
