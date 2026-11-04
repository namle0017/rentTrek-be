using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Post.Requests;
using RenTrek.BLL.DTOs.Post.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Soạn và xuất bản bài viết (O32). Staff chỉ sửa / xóa bài mình tạo; Admin sửa tất cả.
/// </summary>
[ApiController]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
[Route("api/v1/admin/posts")]
[Produces("application/json")]
public sealed class AdminPostsController(IPostService postService, IFileStorageService fileStorage) : ControllerBase
{
    private const long PostImageMaxBytes = 5 * 1024 * 1024;

    /// <summary>Upload ảnh để chèn vào nội dung Markdown của bài viết.</summary>
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PostImageMaxBytes + 64 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<PostImageResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("Vui lòng chọn một file ảnh để tải lên");

        await using var stream = file.OpenReadStream();
        var url = await fileStorage.SaveImageAsync(
            stream, file.FileName, file.ContentType, file.Length, "posts", PostImageMaxBytes, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<PostImageResponse>.Ok(new PostImageResponse(url), "Tải ảnh lên thành công", StatusCodes.Status201Created));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AdminPostListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync([FromQuery] AdminPostQueryRequest query, CancellationToken cancellationToken)
    {
        var result = await postService.GetAdminPostsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResponse<AdminPostListItemResponse>>.Ok(result, "Lấy danh sách bài viết thành công"));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await postService.GetAdminPostByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminPostDetailResponse>.Ok(result, "Lấy bài viết thành công"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminPostDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] UpsertPostRequest request, CancellationToken cancellationToken)
    {
        var result = await postService.CreateAsync(request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AdminPostDetailResponse>.Ok(result, "Đã tạo bản nháp", StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpsertPostRequest request, CancellationToken cancellationToken)
    {
        var result = await postService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<AdminPostDetailResponse>.Ok(result, "Đã lưu bài viết"));
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(ApiResponse<AdminPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PublishAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await postService.PublishAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminPostDetailResponse>.Ok(result, "Đã xuất bản bài viết"));
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(typeof(ApiResponse<AdminPostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ArchiveAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await postService.ArchiveAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminPostDetailResponse>.Ok(result, "Đã lưu trữ bài viết"));
    }

    [HttpGet("{id:guid}/read-analytics")]
    [ProducesResponseType(typeof(ApiResponse<PostReadAnalyticsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReadAnalyticsAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await postService.GetReadAnalyticsAsync(id, cancellationToken);
        return Ok(ApiResponse<PostReadAnalyticsResponse>.Ok(result, "Lấy thống kê lượt đọc thành công"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await postService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Đã xóa bài viết"));
    }
}
