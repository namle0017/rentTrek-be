using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Tour.Requests;
using RenTrek.BLL.DTOs.Tour.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint HTTP cho tours và cấu hình nhóm / lịch lặp theo phiên bản tour.
/// </summary>
[ApiController]
[Route("api/v1/tours")]
[Produces("application/json")]
public class ToursController : ControllerBase
{
    private readonly ITourService _tourService;
    private readonly ITripRecurrenceService _tripRecurrenceService;

    public ToursController(ITourService tourService, ITripRecurrenceService tripRecurrenceService)
    {
        _tourService = tourService;
        _tripRecurrenceService = tripRecurrenceService;
    }

    /// <summary>
    /// Lấy danh sách tour du lịch kèm phân trang, tìm kiếm và bộ lọc đa tiêu chí
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<TourResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResponse<TourResponse>>>> GetToursAsync(
        [FromQuery] TourQueryRequest query,
        CancellationToken cancellationToken)
    {
        var result = await _tourService.GetToursAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResponse<TourResponse>>.Ok(result, "Lấy danh sách tour thành công"));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một tour (kèm ảnh, lịch trình ngày, trang bị và các đợt khởi hành)
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TourDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TourDetailResponse>>> GetTourByIdAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _tourService.GetTourByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<TourDetailResponse>.Ok(result, "Lấy chi tiết tour thành công"));
    }

    /// <summary>
    /// Admin cập nhật sức chứa nhóm chuẩn (GroupMaxParticipants) cho phiên bản Tour.
    /// </summary>
    [HttpPut("{id:guid}/group-settings")]
    [ProducesResponseType(typeof(ApiResponse<TourGroupSettingsResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateGroupSettingsAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateTourGroupSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _tripRecurrenceService.UpdateTourGroupSettingsAsync(id, request, cancellationToken);
        return Ok(ApiResponse<TourGroupSettingsResponse>.Ok(result, "Cập nhật sức chứa nhóm của tour thành công"));
    }

    /// <summary>
    /// Admin tạo quy tắc lịch lặp khởi hành cho Tour (bắt buộc DefaultGroupCount dương).
    /// </summary>
    [HttpPost("{id:guid}/recurrence-rules")]
    [ProducesResponseType(typeof(ApiResponse<TourRecurrenceRuleResponse>), StatusCodes.Status201Created)]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateRecurrenceRuleAsync(
        [FromRoute] Guid id,
        [FromBody] CreateTourRecurrenceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _tripRecurrenceService.CreateRecurrenceRuleAsync(id, request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<TourRecurrenceRuleResponse>.Ok(result, "Tạo quy tắc lịch lặp thành công", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Lấy danh sách quy tắc lịch lặp của Tour.
    /// </summary>
    [HttpGet("{id:guid}/recurrence-rules")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TourRecurrenceRuleResponse>>), StatusCodes.Status200OK)]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> GetRecurrenceRulesAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _tripRecurrenceService.GetRecurrenceRulesAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TourRecurrenceRuleResponse>>.Ok(result, "Lấy danh sách quy tắc lịch lặp thành công"));
    }
}
