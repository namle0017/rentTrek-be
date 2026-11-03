using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalOps.Requests;
using RenTrek.BLL.DTOs.RentalOps.Responses;
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
    public async Task<IActionResult> GetAll([FromQuery] AdminRentalOrderQueryRequest query, CancellationToken cancellationToken)
        => Ok(ApiResponse<PagedResponse<AdminRentalOrderListItemResponse>>.Ok(
            await fulfillment.GetListAsync(query, cancellationToken), "Lấy danh sách đơn thuê thành công"));

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid orderId, CancellationToken cancellationToken)
        => Detail(await fulfillment.GetDetailAsync(orderId, cancellationToken), "Lấy chi tiết đơn thuê thành công");

    [HttpPost("{orderId:guid}/handover")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Handover(
        [FromRoute] Guid orderId,
        [FromBody] HandoverRentalRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.HandoverAsync(orderId, request, cancellationToken), "Đã giao thiết bị cho khách");

    [HttpPost("{orderId:guid}/returns")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Return(
        [FromRoute] Guid orderId,
        [FromBody] ReturnRentalAssetsRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.ReturnAsync(orderId, request, cancellationToken), "Đã ghi nhận trả thiết bị");

    [HttpPost("{orderId:guid}/charges")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCharge(
        [FromRoute] Guid orderId,
        [FromBody] CreateRentalChargeRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.CreateChargeAsync(orderId, request, cancellationToken), "Đã lập khoản phí");

    [HttpPost("{orderId:guid}/charges/{chargeId:guid}/assess")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssessCharge([FromRoute] Guid orderId, [FromRoute] Guid chargeId, CancellationToken cancellationToken)
        => Detail(await fulfillment.AssessChargeAsync(orderId, chargeId, cancellationToken), "Đã xác nhận khoản phí");

    [HttpPost("{orderId:guid}/charges/{chargeId:guid}/void")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VoidCharge(
        [FromRoute] Guid orderId,
        [FromRoute] Guid chargeId,
        [FromBody] VoidRentalChargeRequest request,
        CancellationToken cancellationToken)
        => Detail(await fulfillment.VoidChargeAsync(orderId, chargeId, request, cancellationToken), "Đã hủy khoản phí");

    [HttpPost("{orderId:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<AdminRentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete([FromRoute] Guid orderId, CancellationToken cancellationToken)
        => Detail(await fulfillment.CompleteAsync(orderId, cancellationToken), "Đã đóng đơn thuê");

    private OkObjectResult Detail(AdminRentalOrderDetailResponse detail, string message)
        => Ok(ApiResponse<AdminRentalOrderDetailResponse>.Ok(detail, message));
}
