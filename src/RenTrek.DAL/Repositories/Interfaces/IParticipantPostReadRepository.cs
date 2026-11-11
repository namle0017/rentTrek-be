using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Truy vấn danh sách tham gia chuyến đi của người dùng và trạng thái đọc bài viết của hành khách.
/// </summary>
public interface IParticipantPostReadRepository : IGenericRepository<ParticipantPostRead>
{
    /// <summary>
    /// Liệt kê các lượt tham gia chưa huỷ, chưa xoá mà tài khoản được liên kết, sắp mới nhất trước.
    /// </summary>
    Task<IReadOnlyList<MyParticipationRow>> GetMyParticipationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra hành khách tồn tại, chưa xoá, không bị huỷ và thuộc quyền sở hữu của tài khoản.
    /// </summary>
    Task<bool> IsOwnedActiveParticipantAsync(
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách bài viết Published chưa xoá thuộc tour của chuyến đi mà hành khách tham gia, sắp theo DisplayOrder.
    /// </summary>
    Task<IReadOnlyList<ParticipantPostRow>> GetParticipantPostsAsync(
        Guid participantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra hành khách hợp lệ và bài viết đang Published chưa xoá có gắn với tour của chuyến đi hay không.
    /// </summary>
    Task<bool> CanAccessTourPostAsync(
        Guid participantId,
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc bản ghi đã đọc theo cặp hành khách - bài viết ở chế độ không tracking.
    /// </summary>
    Task<ParticipantPostRead?> GetByParticipantAndPostAsync(
        Guid participantId,
        Guid postId,
        CancellationToken cancellationToken = default);
}
