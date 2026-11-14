using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalReport.Requests;
using RenTrek.BLL.DTOs.RentalReport.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Báo cáo đơn thuê (no-show, huỷ trễ, kiểm tra trả đồ, nghi mất, thu thêm). Staff xem; chỉ Admin duyệt / từ chối.
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/rental-reports")]
[Produces("application/json")]
public sealed class AdminRentalReportsController(IRentalReportService reports) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RentalReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync([FromQuery] RentalReportQueryRequest query, CancellationToken cancellationToken)
        => Ok(ApiResponse<PagedResponse<RentalReportResponse>>.Ok(
            await reports.SearchAsync(query, cancellationToken), "Lấy danh sách báo cáo thành công"));

    [HttpGet("{reportId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RentalReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync([FromRoute] Guid reportId, CancellationToken cancellationToken)
        => Ok(ApiResponse<RentalReportResponse>.Ok(await reports.GetAsync(reportId, cancellationToken), "Lấy báo cáo thành công"));

    /// <summary>
    /// Duyệt: ReturnInspection / SuspectedLoss thì chốt phí và quyết toán cọc (hoàn dư → chờ khách nhập STK, thiếu → thu thêm);
    /// ExtraPayment = xác nhận đã thu ngoài hệ thống; NoShow / LateCancellation = đã xem.
    /// </summary>
    [HttpPost("{reportId:guid}/approve")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<RentalReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAsync(
        [FromRoute] Guid reportId,
        [FromBody] ReviewRentalReportRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<RentalReportResponse>.Ok(await reports.ApproveAsync(reportId, request, cancellationToken), "Đã duyệt báo cáo"));

    /// <summary>Từ chối báo cáo kiểm tra / nghi mất để staff chỉnh phí và gửi lại.</summary>
    [HttpPost("{reportId:guid}/reject")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<RentalReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(
        [FromRoute] Guid reportId,
        [FromBody] ReviewRentalReportRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<RentalReportResponse>.Ok(await reports.RejectAsync(reportId, request, cancellationToken), "Đã từ chối báo cáo"));
}
