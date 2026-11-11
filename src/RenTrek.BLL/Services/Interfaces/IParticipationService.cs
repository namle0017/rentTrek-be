using RenTrek.BLL.DTOs.Participation.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Nghiệp vụ dành cho người tham gia chuyến đi đã liên kết tài khoản: xem danh sách chuyến tham gia và theo dõi đọc bài hướng dẫn.
/// </summary>
public interface IParticipationService
{
    /// <summary>
    /// Liệt kê mọi lượt tham gia chuyến đi mà người dùng hiện tại được liên kết tài khoản, sắp mới nhất trước.
    /// </summary>
    Task<IReadOnlyList<MyParticipationResponse>> GetMyParticipationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách bài viết đã xuất bản thuộc tour của chuyến đi mà hành khách tham gia, kèm trạng thái đã đọc.
    /// </summary>
    Task<IReadOnlyList<ParticipantPostResponse>> GetParticipantPostsAsync(
        Guid participantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu bài viết hướng dẫn của tour là đã đọc cho hành khách hiện tại theo cơ chế idempotent.
    /// </summary>
    Task<ParticipantPostReadResponse> MarkPostAsReadAsync(
        Guid participantId,
        Guid postId,
        CancellationToken cancellationToken = default);
}
