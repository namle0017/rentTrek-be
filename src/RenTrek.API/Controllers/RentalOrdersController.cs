using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho rental orders.
/// </summary>
[ApiController]
[Route("api/v1/rental-orders")]
public class RentalOrdersController : ControllerBase
{
    private readonly IRentalOrderService _rentalOrderService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRentalCancellationService _cancellationService;
    private readonly IRentalLifecycleService _lifecycleService;

    public RentalOrdersController(
        IRentalOrderService rentalOrderService,
        ICurrentUserService currentUserService,
        IRentalCancellationService cancellationService,
        IRentalLifecycleService lifecycleService)
    {
        _rentalOrderService = rentalOrderService;
        _currentUserService = currentUserService;
        _cancellationService = cancellationService;
        _lifecycleService = lifecycleService;
    }

    /// <summary>Báo giá huỷ đơn đã thanh toán (phí theo chính sách cọc, số tiền hoàn); không ghi dữ liệu.</summary>
    [HttpGet("{orderId:guid}/cancellation-preview")]
    [ProducesResponseType(typeof(ApiResponse<RentalCancellationPreviewResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> GetCancellationPreviewAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _cancellationService.PreviewAsync(_currentUserService.RequiredUserId, orderId, cancellationToken);
        return Ok(ApiResponse<RentalCancellationPreviewResponse>.Ok(result, "Lấy báo giá huỷ đơn thành công"));
    }

    /// <summary>Khách huỷ đơn đã thanh toán, chưa nhận đồ; có tiền hoàn thì bắt buộc STK + đồng ý xử lý dữ liệu.</summary>
    [HttpPost("{orderId:guid}/cancel-paid")]
    [EnableRateLimiting(RateLimitPolicies.RefundSensitive)]
    [ProducesResponseType(typeof(ApiResponse<RentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> CancelPaidAsync(
        Guid orderId,
        [FromBody] CancelPaidRentalOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cancellationService.CancelPaidAsync(_currentUserService.RequiredUserId, orderId, request, cancellationToken);
        return Ok(ApiResponse<RentalOrderDetailResponse>.Ok(result, "Đã huỷ đơn thuê"));
    }

    /// <summary>Khách gửi ảnh khi báo lỗi lúc nhận đồ (lưu làm bằng chứng CustomerIssue).</summary>
    [HttpPost("{orderId:guid}/issue-photos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<RentalEvidencePhotoResponse>), StatusCodes.Status201Created)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> UploadIssuePhotoAsync(
        Guid orderId,
        IFormFile file,
        [FromForm] Guid? assignmentId,
        [FromForm] string? note,
        [FromServices] IRentalEvidenceService evidence,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new RenTrek.BLL.Exceptions.ValidationException("Vui lòng chọn ảnh");
        await using var stream = file.OpenReadStream();
        var photo = await evidence.UploadCustomerAsync(
            _currentUserService.RequiredUserId, orderId, assignmentId, note, stream, file.FileName, file.ContentType, file.Length, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<RentalEvidencePhotoResponse>.Ok(photo, "Đã gửi ảnh", StatusCodes.Status201Created));
    }

    /// <summary>Khách xác nhận đã nhận đồ và đã kiểm tra kỹ.</summary>
    [HttpPost("{orderId:guid}/confirm-receipt")]
    [ProducesResponseType(typeof(ApiResponse<RentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> ConfirmReceiptAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await _lifecycleService.ConfirmReceiptAsync(_currentUserService.RequiredUserId, orderId, cancellationToken);
        return Ok(ApiResponse<RentalOrderDetailResponse>.Ok(result, "Đã xác nhận nhận đồ"));
    }

    [HttpPost("quote")]
    [ProducesResponseType(typeof(ApiResponse<RentalQuoteResponse>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> QuoteAsync([FromBody] RentalQuoteRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.QuoteAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<RentalQuoteResponse>.Ok(result, "Báo giá đơn thuê thành công"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RentalOrderDetailResponse>), StatusCodes.Status201Created)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateRentalOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.CreateAsync(userId, request, idempotencyKey, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<RentalOrderDetailResponse>.Ok(result, "Tạo đơn thuê thành công", StatusCodes.Status201Created));
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.GetByIdAsync(userId, orderId, cancellationToken);
        return Ok(ApiResponse<RentalOrderDetailResponse>.Ok(result, "Lấy chi tiết đơn thuê thành công"));
    }

    /// <summary>Báo giá gia hạn ngày trả cho đơn thuê của chính khách (FR-R09); không ghi dữ liệu.</summary>
    [HttpPost("{orderId:guid}/extensions/quote")]
    [ProducesResponseType(typeof(ApiResponse<RentalExtensionQuoteResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> QuoteExtensionAsync(
        Guid orderId,
        [FromBody] ExtendRentalQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.QuoteExtensionAsync(userId, orderId, request, cancellationToken);
        return Ok(ApiResponse<RentalExtensionQuoteResponse>.Ok(result, "Báo giá gia hạn đơn thuê thành công"));
    }

    /// <summary>Áp dụng gia hạn khi khách đồng ý đúng số tiền đã báo giá (FR-R09, UAT-19).</summary>
    [HttpPost("{orderId:guid}/extensions")]
    [ProducesResponseType(typeof(ApiResponse<RentalExtensionResultResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> ExtendAsync(
        Guid orderId,
        [FromBody] ExtendRentalOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.ExtendAsync(userId, orderId, request, cancellationToken);
        return Ok(ApiResponse<RentalExtensionResultResponse>.Ok(result, "Gia hạn đơn thuê thành công"));
    }

    /// <summary>Lịch sử điều chỉnh (gia hạn) của đơn thuê thuộc khách.</summary>
    [HttpGet("{orderId:guid}/adjustments")]
    [ProducesResponseType(typeof(ApiResponse<List<RentalOrderAdjustmentResponse>>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> GetAdjustmentsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.GetAdjustmentsAsync(userId, orderId, cancellationToken);
        return Ok(ApiResponse<List<RentalOrderAdjustmentResponse>>.Ok(result, "Lấy lịch sử điều chỉnh đơn thuê thành công"));
    }

    /// <summary>Khách hủy đơn thuê chưa thanh toán (PendingPayment + Unpaid) và giải phóng thiết bị đang giữ.</summary>
    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<RentalOrderDetailResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> CancelUnpaidAsync(
        Guid orderId,
        [FromBody] CancelRentalOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.RequiredUserId;

        var result = await _rentalOrderService.CancelUnpaidAsync(userId, orderId, request, cancellationToken);
        return Ok(ApiResponse<RentalOrderDetailResponse>.Ok(result, "Đã hủy đơn thuê chưa thanh toán và giải phóng thiết bị."));
    }
}
