using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/tours")]
public sealed class AdminToursController(ITourService tourService, IAdminTourService adminTourService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminTourListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromQuery] AdminTourQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await tourService.GetAdminToursAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminTourListItemResponse>>.Ok(result, "Lấy danh sách tour vận hành thành công"));
    }

    /// <summary>6.2. Tạo tour Draft; trả id.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveTourRequest request, CancellationToken cancellationToken)
    {
        var result = await adminTourService.CreateTourAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo tour thành công", StatusCodes.Status201Created));
    }

    /// <summary>6.3. Thông tin chung tour kèm cờ khóa lịch trình.</summary>
    [HttpGet("{tourId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminTourDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.GetTourAsync(tourId, cancellationToken);
        return Ok(ApiResponse<AdminTourDetailResponse>.Ok(result, "Lấy thông tin tour thành công"));
    }

    /// <summary>6.4. Sửa thông tin chung (400 ITINERARY_LOCKED nếu đổi số ngày khi đã khóa).</summary>
    [HttpPut("{tourId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid tourId, [FromBody] SaveTourRequest request,
        CancellationToken cancellationToken)
    {
        await adminTourService.UpdateTourAsync(tourId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật tour thành công"));
    }

    /// <summary>Xoá mềm cả họ tour; chỉ khi chưa có chuyến (ngoài contract, thay cho xoá từng phiên bản).</summary>
    [HttpDelete("{tourId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        await adminTourService.DeleteTourAsync(tourId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá tour thành công"));
    }

    /// <summary>6.5. Xuất bản tour.</summary>
    [HttpPost("{tourId:guid}/publish")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        await adminTourService.PublishTourAsync(tourId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xuất bản tour thành công"));
    }

    /// <summary>6.6. Lưu trữ / ẩn tour.</summary>
    [HttpPost("{tourId:guid}/archive")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ArchiveAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        await adminTourService.ArchiveTourAsync(tourId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Lưu trữ tour thành công"));
    }

    /// <summary>6.7. Danh sách ảnh tour.</summary>
    [HttpGet("{tourId:guid}/images")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminTourImageResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImagesAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.GetImagesAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminTourImageResponse>>.Ok(result, "Lấy ảnh tour thành công"));
    }

    /// <summary>6.8. Thêm ảnh vào tour; trả id.</summary>
    [HttpPost("{tourId:guid}/images")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddImageAsync([FromRoute] Guid tourId, [FromBody] SaveImageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTourService.AddImageAsync(tourId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Thêm ảnh thành công", StatusCodes.Status201Created));
    }

    /// <summary>6.9. Sắp xếp ảnh và chọn ảnh đại diện.</summary>
    [HttpPut("{tourId:guid}/images/reorder")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderImagesAsync([FromRoute] Guid tourId, [FromBody] ReorderTourImagesRequest request,
        CancellationToken cancellationToken)
    {
        await adminTourService.ReorderImagesAsync(tourId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Sắp xếp ảnh thành công"));
    }

    /// <summary>6.10. Xoá ảnh khỏi tour.</summary>
    [HttpDelete("{tourId:guid}/images/{imageId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteImageAsync([FromRoute] Guid tourId, [FromRoute] Guid imageId,
        CancellationToken cancellationToken)
    {
        await adminTourService.DeleteImageAsync(tourId, imageId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá ảnh thành công"));
    }

    /// <summary>6.11. Lịch trình các ngày của tour.</summary>
    [HttpGet("{tourId:guid}/activities")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminTourActivityResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivitiesAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.GetActivitiesAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminTourActivityResponse>>.Ok(result, "Lấy lịch trình thành công"));
    }

    /// <summary>6.12. Thêm hoạt động (400 ITINERARY_LOCKED nếu đã khóa); trả id.</summary>
    [HttpPost("{tourId:guid}/activities")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddActivityAsync([FromRoute] Guid tourId, [FromBody] SaveTourActivityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminTourService.AddActivityAsync(tourId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Thêm hoạt động thành công", StatusCodes.Status201Created));
    }

    /// <summary>6.15. Sắp xếp lại hoạt động trong một ngày (400 ITINERARY_LOCKED nếu đã khóa).</summary>
    [HttpPut("{tourId:guid}/activities/reorder")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderActivitiesAsync([FromRoute] Guid tourId,
        [FromBody] ReorderTourActivitiesRequest request, CancellationToken cancellationToken)
    {
        await adminTourService.ReorderActivitiesAsync(tourId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Sắp xếp hoạt động thành công"));
    }

    /// <summary>6.13. Sửa hoạt động (400 ITINERARY_LOCKED nếu đã khóa).</summary>
    [HttpPut("{tourId:guid}/activities/{activityId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateActivityAsync([FromRoute] Guid tourId, [FromRoute] Guid activityId,
        [FromBody] SaveTourActivityRequest request, CancellationToken cancellationToken)
    {
        await adminTourService.UpdateActivityAsync(tourId, activityId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật hoạt động thành công"));
    }

    /// <summary>6.14. Xoá hoạt động (400 ITINERARY_LOCKED nếu đã khóa).</summary>
    [HttpDelete("{tourId:guid}/activities/{activityId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteActivityAsync([FromRoute] Guid tourId, [FromRoute] Guid activityId,
        CancellationToken cancellationToken)
    {
        await adminTourService.DeleteActivityAsync(tourId, activityId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá hoạt động thành công"));
    }

    /// <summary>6.16. Danh sách đồ tự mang.</summary>
    [HttpGet("{tourId:guid}/preparation-items")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminTourPreparationItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreparationItemsAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.GetPreparationItemsAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminTourPreparationItemResponse>>.Ok(result, "Lấy đồ tự mang thành công"));
    }

    /// <summary>6.17. Thay toàn bộ đồ tự mang (400 ITINERARY_LOCKED nếu đã khóa).</summary>
    [HttpPut("{tourId:guid}/preparation-items")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPreparationItemsAsync([FromRoute] Guid tourId,
        [FromBody] TourPreparationItemsRequest request, CancellationToken cancellationToken)
    {
        await adminTourService.SetPreparationItemsAsync(tourId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật đồ tự mang thành công"));
    }

    /// <summary>6.18. Danh sách đồ cấp cấu hình cho tour.</summary>
    [HttpGet("{tourId:guid}/provided-items")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminTourProvidedItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProvidedItemsAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.GetProvidedItemsAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminTourProvidedItemResponse>>.Ok(result, "Lấy đồ cấp thành công"));
    }

    /// <summary>6.19. Thêm định mức đồ cấp (400 ITINERARY_LOCKED nếu đã khóa); trả id.</summary>
    [HttpPost("{tourId:guid}/provided-items")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddProvidedItemAsync([FromRoute] Guid tourId,
        [FromBody] SaveTourProvidedItemRequest request, CancellationToken cancellationToken)
    {
        var result = await adminTourService.AddProvidedItemAsync(tourId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Thêm đồ cấp thành công", StatusCodes.Status201Created));
    }

    /// <summary>6.20. Sửa định mức đồ cấp (400 ITINERARY_LOCKED nếu đã khóa).</summary>
    [HttpPut("{tourId:guid}/provided-items/{itemId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProvidedItemAsync([FromRoute] Guid tourId, [FromRoute] Guid itemId,
        [FromBody] SaveTourProvidedItemRequest request, CancellationToken cancellationToken)
    {
        await adminTourService.UpdateProvidedItemAsync(tourId, itemId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật đồ cấp thành công"));
    }

    /// <summary>6.21. Xoá định mức đồ cấp (400 ITINERARY_LOCKED nếu đã khóa).</summary>
    [HttpDelete("{tourId:guid}/provided-items/{itemId:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteProvidedItemAsync([FromRoute] Guid tourId, [FromRoute] Guid itemId,
        CancellationToken cancellationToken)
    {
        await adminTourService.DeleteProvidedItemAsync(tourId, itemId, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá đồ cấp thành công"));
    }

    /// <summary>6.22. Bài viết cẩm nang gắn tour, theo thứ tự đọc.</summary>
    [HttpGet("{tourId:guid}/posts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminTourPostResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPostsAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.GetPostsAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminTourPostResponse>>.Ok(result, "Lấy bài viết của tour thành công"));
    }

    /// <summary>6.23. Thay toàn bộ bài viết gắn tour và thứ tự đọc.</summary>
    [HttpPut("{tourId:guid}/posts")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPostsAsync([FromRoute] Guid tourId,
        [FromBody] TourPostsRequest request, CancellationToken cancellationToken)
    {
        await adminTourService.SetPostsAsync(tourId, request, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Cập nhật bài viết của tour thành công"));
    }

    /// <summary>6.24. Lịch sử phiên bản trong họ tour.</summary>
    [HttpGet("{tourId:guid}/versions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminTourVersionResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersionsAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.GetVersionsAsync(tourId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminTourVersionResponse>>.Ok(result, "Lấy lịch sử phiên bản thành công"));
    }

    /// <summary>6.25. Clone sang phiên bản mới (Draft, chưa khóa); trả id bản mới.</summary>
    [HttpPost("{tourId:guid}/versions/clone")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CloneVersionAsync([FromRoute] Guid tourId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.CloneVersionAsync(tourId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Guid>.Ok(result, "Tạo phiên bản mới thành công", StatusCodes.Status201Created));
    }

    /// <summary>6.26. So sánh phiên bản này với một phiên bản khác cùng họ.</summary>
    [HttpGet("{tourId:guid}/versions/diff")]
    [ProducesResponseType(typeof(ApiResponse<AdminTourDiffResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DiffVersionsAsync([FromRoute] Guid tourId,
        [FromQuery] Guid compareWithVersionId, CancellationToken cancellationToken)
    {
        var result = await adminTourService.DiffVersionsAsync(tourId, compareWithVersionId, cancellationToken);
        return Ok(ApiResponse<AdminTourDiffResponse>.Ok(result, "So sánh phiên bản thành công"));
    }
}
