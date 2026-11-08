namespace RenTrek.BLL.DTOs.Notification.Responses;

/// <summary>
/// Kết quả đánh dấu đã đọc toàn bộ thông báo chưa đọc của người dùng hiện tại.
/// </summary>
public sealed class MarkAllNotificationsReadResponse
{
    public int MarkedCount { get; set; }
}
