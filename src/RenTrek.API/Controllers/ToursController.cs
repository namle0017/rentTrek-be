using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Tour;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/tours")]
[Produces("application/json")]
public class ToursController : ControllerBase
{
    private readonly ITourService _tourService;

    public ToursController(ITourService tourService)
    {
        _tourService = tourService;
    }

    /// <summary>
    /// Lấy danh sách tour du lịch kèm phân trang, tìm kiếm và bộ lọc đa tiêu chí
    /// </summary>
    /// <param name="query">Các tham số phân trang, tìm kiếm, lọc theo danh mục, địa điểm, giá, độ khó</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Danh sách tour phân trang bọc trong ApiResponse chuẩn</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TourResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<TourResponse>>>> GetTours(
        [FromQuery] TourQueryRequest query,
        CancellationToken cancellationToken)
    {
        var result = await _tourService.GetToursAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<TourResponse>>.Ok(result, "Lấy danh sách tour thành công"));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một tour (kèm ảnh, lịch trình ngày, trang bị và các đợt khởi hành)
    /// </summary>
    /// <param name="id">Mã định danh (GUID) của tour</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dữ liệu chi tiết tour bọc trong ApiResponse chuẩn</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TourDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TourDetailResponse>>> GetTourById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _tourService.GetTourByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<TourDetailResponse>.Ok(result, "Lấy chi tiết tour thành công"));
    }
}
