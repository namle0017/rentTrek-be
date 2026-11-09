using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Notification.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Quản lý hàng đợi và truy vấn thông báo trong ứng dụng cho người dùng.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Thêm một thông báo in-app vào UnitOfWork (không gọi SaveChanges) và bỏ qua nếu cặp (recipient, eventKey) đã tồn tại.
    /// </summary>
    Task QueueAsync(
        Guid recipientUserId,
        string type,
        string title,
        string content,
        string relatedEntityType,
        Guid relatedEntityId,
        string eventKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thông báo của người dùng hiện tại (mới nhất trước).
    /// </summary>
    Task<PagedResponse<NotificationItemResponse>> GetMyNotificationsAsync(
        bool? unreadOnly,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm số thông báo chưa đọc của người dùng hiện tại.
    /// </summary>
    Task<UnreadNotificationCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu một thông báo thuộc người dùng hiện tại là đã đọc (idempotent, giữ nguyên ReadAt nếu đã đọc).
    /// </summary>
    Task<MarkNotificationReadResponse> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu toàn bộ thông báo chưa đọc thuộc người dùng hiện tại là đã đọc.
    /// </summary>
    Task<MarkAllNotificationsReadResponse> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}
