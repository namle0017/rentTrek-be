namespace RenTrek.BLL.DTOs.Notification.Responses;

/// <summary>
/// Kết quả đánh dấu đã đọc một thông báo.
/// </summary>
public sealed class MarkNotificationReadResponse
{
    public Guid NotificationId { get; set; }
    public bool IsRead { get; set; }
    public DateTime ReadAt { get; set; }
}
