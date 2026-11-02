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
    public async Task<IActionResult> GetByTour(
        Guid tourId,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.GetRulesByTourAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RecurrenceRuleResponse>>.Ok(result, "Thành công"));
    }

    [HttpPost("tours/{tourId:guid}/recurrence-rules")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Create(
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
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Update(
        Guid ruleId,
        [FromBody] UpdateRecurrenceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.UpdateRuleAsync(ruleId, request, cancellationToken);
        return Ok(ApiResponse<RecurrenceRuleResponse>.Ok(result, "Cập nhật quy tắc lặp chuyến thành công"));
    }

    [HttpDelete("recurrence-rules/{ruleId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        await recurrenceRuleService.DeleteRuleAsync(ruleId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xóa quy tắc lặp chuyến thành công"));
    }

    [HttpPost("recurrence-rules/{ruleId:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid ruleId,
        [FromBody] PreviewRecurrenceTripsRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.PreviewTripsAsync(ruleId, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RecurrencePreviewItemResponse>>.Ok(result, "Thành công"));
    }

    [HttpPost("recurrence-rules/{ruleId:guid}/generate")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Generate(
        Guid ruleId,
        [FromBody] GenerateRecurrenceTripsRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await recurrenceRuleService.GenerateTripsAsync(ruleId, request, cancellationToken);
        return Ok(ApiResponse<GenerateRecurrenceTripsResponse>.Ok(result, "Sinh chuyến tự động thành công"));
    }
}
