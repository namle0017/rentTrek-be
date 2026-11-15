using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.CustomerReport.Requests;
using RenTrek.BLL.DTOs.CustomerReport.Responses;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Khách gửi báo cáo về đơn thuê (sự cố thiết bị) hoặc chuyến đi và theo dõi trạng thái xử lý.
/// </summary>
[ApiController]
[Authorize(Roles = RoleNames.Customer)]
[Route("api/v1/customer-reports")]
[Produces("application/json")]
public sealed class CustomerReportsController(ICustomerReportService reports) : ControllerBase
{
    /// <summary>Tải một ảnh minh hoạ (JPG/PNG/WebP, tối đa 5MB); dùng link trả về trong photoUrls.</summary>
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(CustomerReportService.MaxPhotoBytes + 64 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<CustomerReportImageResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new RenTrek.BLL.Exceptions.ValidationException("Vui lòng chọn một file ảnh để tải lên");

        await using var stream = file.OpenReadStream();
        var result = await reports.UploadImageAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CustomerReportImageResponse>.Ok(result, "Tải ảnh lên thành công", StatusCodes.Status201Created));
    }

    /// <summary>Gửi báo cáo cho đơn thuê (chủ đơn) hoặc booking chuyến (chủ booking / người tham gia).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CustomerReportResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCustomerReportRequest request, CancellationToken cancellationToken)
    {
        var result = await reports.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CustomerReportResponse>.Ok(result, "Đã gửi báo cáo tới RenTrek", StatusCodes.Status201Created));
    }

    /// <summary>Báo cáo của tôi, lọc theo đơn nếu truyền orderId.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineAsync([FromQuery] Guid? orderId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<CustomerReportResponse>>.Ok(
            await reports.GetMineAsync(orderId, cancellationToken), "Lấy báo cáo của bạn thành công"));
}
