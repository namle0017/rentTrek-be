using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Truy vấn và thao tác dữ liệu thông báo trong ứng dụng.
/// </summary>
public interface INotificationRepository : IGenericRepository<Notification>
{
    /// <summary>
    /// Kiểm tra cặp (recipientUserId, eventKey) đã tồn tại trong DB hoặc đang chờ lưu hay chưa.
    /// </summary>
    Task<bool> ExistsByEventKeyAsync(Guid recipientUserId, string eventKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thông báo phân trang của người dùng, mới nhất trước.
    /// </summary>
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedForUserAsync(
        Guid recipientUserId,
        bool? unreadOnly,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm số thông báo chưa đọc của người dùng.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy một thông báo thuộc đúng người nhận để cập nhật trạng thái đọc.
    /// </summary>
    Task<Notification?> GetOwnedForUpdateAsync(
        Guid notificationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy toàn bộ thông báo chưa đọc của người dùng để đánh dấu đã đọc hàng loạt.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetUnreadOwnedForUpdateAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);
}
