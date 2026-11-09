namespace RenTrek.BLL.DTOs.Notification.Responses;

/// <summary>
/// Thông tin một thông báo trong danh sách thông báo của người dùng hiện tại.
/// </summary>
public sealed class NotificationItemResponse
{
    public Guid NotificationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
