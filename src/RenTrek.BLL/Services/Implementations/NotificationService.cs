using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Notification.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ hàng đợi, truy vấn và đánh dấu đã đọc thông báo trong ứng dụng.
/// </summary>
public sealed class NotificationService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : INotificationService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int MaxNotificationTypeLength = 50;
    private const int MaxTitleLength = 255;
    private const int MaxContentLength = 2000;
    private const int MaxRelatedEntityTypeLength = 50;
    private const int MaxEventKeyLength = 200;

    /// <summary>
    /// Thêm một thông báo vào UnitOfWork (chưa gọi SaveChanges) và bỏ qua nếu cặp (recipient, eventKey) đã tồn tại.
    /// </summary>
    public async Task QueueAsync(
        Guid recipientUserId,
        string type,
        string title,
        string content,
        string relatedEntityType,
        Guid relatedEntityId,
        string eventKey,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserId == Guid.Empty)
            return;

        var normalizedEventKey = string.IsNullOrWhiteSpace(eventKey)
            ? null
            : Truncate(eventKey.Trim(), MaxEventKeyLength);

        if (normalizedEventKey is not null &&
            await unitOfWork.Notifications.ExistsByEventKeyAsync(recipientUserId, normalizedEventKey, cancellationToken))
        {
            return;
        }

        var notification = new Notification
        {
            NotificationId = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            NotificationType = Truncate(type.Trim(), MaxNotificationTypeLength),
            Title = Truncate(title.Trim(), MaxTitleLength),
            Content = Truncate(content.Trim(), MaxContentLength),
            RelatedEntityType = string.IsNullOrWhiteSpace(relatedEntityType)
                ? null
                : Truncate(relatedEntityType.Trim(), MaxRelatedEntityTypeLength),
            RelatedEntityId = relatedEntityId == Guid.Empty ? null : relatedEntityId,
            EventKey = normalizedEventKey,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        await unitOfWork.Notifications.AddAsync(notification, cancellationToken);
    }

    /// <summary>
    /// Lấy danh sách thông báo của người dùng hiện tại theo thứ tự mới nhất trước.
    /// </summary>
    public async Task<PagedResponse<NotificationItemResponse>> GetMyNotificationsAsync(
        bool? unreadOnly,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var (items, totalCount) = await unitOfWork.Notifications.GetPagedForUserAsync(
            userId,
            unreadOnly,
            safePage,
            safePageSize,
            cancellationToken);

        return new PagedResponse<NotificationItemResponse>
        {
            Items = items.Select(MapItem).ToList(),
            PageNumber = safePage,
            PageSize = safePageSize,
            TotalItems = totalCount
        };
    }

    /// <summary>
    /// Đếm số lượng thông báo chưa đọc của người dùng hiện tại.
    /// </summary>
    public async Task<UnreadNotificationCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var count = await unitOfWork.Notifications.GetUnreadCountAsync(currentUser.RequiredUserId, cancellationToken);
        return new UnreadNotificationCountResponse { UnreadCount = count };
    }

    /// <summary>
    /// Đánh dấu một thông báo của người dùng hiện tại là đã đọc, giữ nguyên ReadAt nếu đã đọc trước đó.
    /// </summary>
    public async Task<MarkNotificationReadResponse> MarkAsReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var notification = await unitOfWork.Notifications.GetOwnedForUpdateAsync(notificationId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy thông báo");

        if (notification.ReadAt is null)
        {
            notification.ReadAt = timeProvider.GetUtcNow().UtcDateTime;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new MarkNotificationReadResponse
        {
            NotificationId = notification.NotificationId,
            IsRead = true,
            ReadAt = notification.ReadAt.Value
        };
    }

    /// <summary>
    /// Đánh dấu toàn bộ thông báo chưa đọc của người dùng hiện tại là đã đọc và trả về số lượng vừa cập nhật.
    /// </summary>
    public async Task<MarkAllNotificationsReadResponse> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var unread = await unitOfWork.Notifications.GetUnreadOwnedForUpdateAsync(userId, cancellationToken);
        if (unread.Count > 0)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var notification in unread)
                notification.ReadAt = now;

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new MarkAllNotificationsReadResponse { MarkedCount = unread.Count };
    }

    private static NotificationItemResponse MapItem(Notification notification)
        => new()
        {
            NotificationId = notification.NotificationId,
            Type = notification.NotificationType,
            Title = notification.Title,
            Content = notification.Content,
            IsRead = notification.ReadAt.HasValue,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId
        };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
