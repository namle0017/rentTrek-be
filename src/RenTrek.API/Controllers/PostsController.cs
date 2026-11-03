using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Post.Requests;
using RenTrek.BLL.DTOs.Post.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Bài viết / cẩm nang công khai (C17, C18). Chỉ trả bài đã xuất bản.
/// </summary>
[ApiController]
[Route("api/v1/posts")]
[Produces("application/json")]
public sealed class PostsController(IPostService postService) : ControllerBase
{
    /// <summary>Danh sách bài đã xuất bản, lọc theo loại / tag slug / kỹ năng.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PostListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PostQueryRequest query, CancellationToken cancellationToken)
    {
        var result = await postService.GetPublishedPostsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResponse<PostListItemResponse>>.Ok(result, "Lấy danh sách bài viết thành công"));
    }

    /// <summary>Danh sách tag và kỹ năng mềm dùng cho bộ lọc / trình soạn.</summary>
    [HttpGet("taxonomy")]
    [ProducesResponseType(typeof(ApiResponse<PostTaxonomyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaxonomy(CancellationToken cancellationToken)
    {
        var result = await postService.GetTaxonomyAsync(cancellationToken);
        return Ok(ApiResponse<PostTaxonomyResponse>.Ok(result, "Lấy danh sách tag và kỹ năng thành công"));
    }

    /// <summary>Chi tiết bài viết theo slug.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<PostDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug([FromRoute] string slug, CancellationToken cancellationToken)
    {
        var result = await postService.GetPublishedPostBySlugAsync(slug, cancellationToken);
        return Ok(ApiResponse<PostDetailResponse>.Ok(result, "Lấy chi tiết bài viết thành công"));
    }
}
