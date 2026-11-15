using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.CustomerReport.Requests;
using RenTrek.BLL.DTOs.CustomerReport.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Màn "Báo cáo từ khách hàng": Admin / Staff xem báo cáo về đơn thuê và chuyến đi, cập nhật trạng thái xử lý.
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/customer-reports")]
[Produces("application/json")]
public sealed class AdminCustomerReportsController(ICustomerReportService reports) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<CustomerReportResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync([FromQuery] CustomerReportQueryRequest query, CancellationToken cancellationToken)
        => Ok(ApiResponse<PagedResponse<CustomerReportResponse>>.Ok(
            await reports.SearchAsync(query, cancellationToken), "Lấy danh sách báo cáo thành công"));

    /// <summary>Số báo cáo theo trạng thái.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<CustomerReportSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
        => Ok(ApiResponse<CustomerReportSummaryResponse>.Ok(
            await reports.GetSummaryAsync(cancellationToken), "Lấy tổng quan báo cáo thành công"));

    [HttpGet("{reportId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync([FromRoute] Guid reportId, CancellationToken cancellationToken)
        => Ok(ApiResponse<CustomerReportResponse>.Ok(await reports.GetAsync(reportId, cancellationToken), "Lấy báo cáo thành công"));

    /// <summary>Chuyển InProgress / Resolved / Rejected; đóng báo cáo phải kèm phản hồi cho khách.</summary>
    [HttpPatch("{reportId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<CustomerReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatusAsync(
        [FromRoute] Guid reportId,
        [FromBody] UpdateCustomerReportStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<CustomerReportResponse>.Ok(
            await reports.UpdateStatusAsync(reportId, request, cancellationToken), "Đã cập nhật báo cáo"));
}
