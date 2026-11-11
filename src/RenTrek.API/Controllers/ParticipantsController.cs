using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Participation.Responses;
using RenTrek.BLL.DTOs.Review.Requests;
using RenTrek.BLL.DTOs.Review.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint cho người tham gia chuyến đi đã liên kết tài khoản (đọc bài viết tour, gửi/sửa đánh giá).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/participants")]
[Produces("application/json")]
public sealed class ParticipantsController(
    IParticipationService participationService,
    IReviewService reviewService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách bài viết đã xuất bản thuộc tour của chuyến đi mà người tham gia được liên kết, kèm trạng thái đã đọc.
    /// </summary>
    [HttpGet("{participantId:guid}/posts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParticipantPostResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostsAsync([FromRoute] Guid participantId, CancellationToken cancellationToken)
    {
        var result = await participationService.GetParticipantPostsAsync(participantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParticipantPostResponse>>.Ok(result, "Lấy danh sách bài viết của chuyến đi thành công"));
    }

    /// <summary>
    /// Đánh dấu một bài viết của tour là đã đọc cho người tham gia (idempotent, gửi lại vẫn giữ nguyên thời điểm đọc ban đầu).
    /// </summary>
    [HttpPost("{participantId:guid}/posts/{postId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<ParticipantPostReadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkPostAsReadAsync(
        [FromRoute] Guid participantId,
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var result = await participationService.MarkPostAsReadAsync(participantId, postId, cancellationToken);
        return Ok(ApiResponse<ParticipantPostReadResponse>.Ok(result, "Đánh dấu đã đọc bài viết thành công"));
    }

    /// <summary>
    /// Người tham gia đã hoàn thành chuyến gửi đánh giá 1–5 sao đầu tiên cho chuyến đi.
    /// </summary>
    [HttpPost("{participantId:guid}/review")]
    [ProducesResponseType(typeof(ApiResponse<ParticipantReviewResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReviewAsync(
        [FromRoute] Guid participantId,
        [FromBody] SaveReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.CreateReviewAsync(participantId, request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<ParticipantReviewResponse>.Ok(result, "Gửi đánh giá chuyến đi thành công", StatusCodes.Status201Created));
    }

    /// <summary>
    /// Người tham gia chỉnh sửa đánh giá của mình trong khoảng thời gian cho phép kể từ lúc tạo.
    /// </summary>
    [HttpPut("{participantId:guid}/review")]
    [ProducesResponseType(typeof(ApiResponse<ParticipantReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateReviewAsync(
        [FromRoute] Guid participantId,
        [FromBody] SaveReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.UpdateReviewAsync(participantId, request, cancellationToken);
        return Ok(ApiResponse<ParticipantReviewResponse>.Ok(result, "Cập nhật đánh giá chuyến đi thành công"));
    }
}
