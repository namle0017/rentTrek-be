using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP quản lý lịch lặp và tự động sinh chuyến đi cho tour (Màn O08 - Mục 7).
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin")]
public sealed class AdminRecurrenceRulesController(
    IAdminRecurrenceRuleService recurrenceRuleService) : ControllerBase
{
    [HttpGet("tours/{tourId:guid}/recurrence-rules")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecurrenceRuleResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTourAsync(
        Guid tourId,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.GetRulesByTourAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RecurrenceRuleResponse>>.Ok(result, "Lấy danh sách lịch lặp thành công"));
    }

    [HttpPost("tours/{tourId:guid}/recurrence-rules")]
    [ProducesResponseType(typeof(ApiResponse<RecurrenceRuleResponse>), StatusCodes.Status201Created)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateAsync(
        Guid tourId,
        [FromBody] CreateRecurrenceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.CreateRuleAsync(tourId, request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<RecurrenceRuleResponse>.Ok(result, "Tạo quy tắc lặp chuyến thành công", StatusCodes.Status201Created));
    }

    [HttpPut("recurrence-rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RecurrenceRuleResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateAsync(
        Guid ruleId,
        [FromBody] UpdateRecurrenceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.UpdateRuleAsync(ruleId, request, cancellationToken);
        return Ok(ApiResponse<RecurrenceRuleResponse>.Ok(result, "Cập nhật quy tắc lặp chuyến thành công"));
    }

    [HttpDelete("recurrence-rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> DeleteAsync(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        await recurrenceRuleService.DeleteRuleAsync(ruleId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xóa quy tắc lặp chuyến thành công"));
    }

    [HttpPost("recurrence-rules/{ruleId:guid}/preview")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecurrencePreviewItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewAsync(
        Guid ruleId,
        [FromBody] PreviewRecurrenceTripsRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.PreviewTripsAsync(ruleId, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RecurrencePreviewItemResponse>>.Ok(result, "Xem trước lịch khởi hành thành công"));
    }

    [HttpPost("recurrence-rules/{ruleId:guid}/generate")]
    [ProducesResponseType(typeof(ApiResponse<GenerateRecurrenceTripsResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GenerateAsync(
        Guid ruleId,
        [FromBody] GenerateRecurrenceTripsRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.GenerateTripsAsync(ruleId, request, cancellationToken);
        return Ok(ApiResponse<GenerateRecurrenceTripsResponse>.Ok(result, "Sinh chuyến tự động thành công"));
    }
}
