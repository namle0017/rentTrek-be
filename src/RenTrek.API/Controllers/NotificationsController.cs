using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Notification.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

/// <summary>
/// Cung cấp các endpoint quản lý thông báo trong ứng dụng của người dùng hiện tại.
/// </summary>
[ApiController]
[Route("api/v1/users/me/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách thông báo của người dùng hiện tại, mới nhất trước.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<NotificationItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotificationsAsync(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await notificationService.GetMyNotificationsAsync(unreadOnly, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResponse<NotificationItemResponse>>.Ok(result, "Lấy danh sách thông báo thành công"));
    }

    /// <summary>
    /// Đếm số lượng thông báo chưa đọc của người dùng hiện tại.
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<UnreadNotificationCountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var result = await notificationService.GetUnreadCountAsync(cancellationToken);
        return Ok(ApiResponse<UnreadNotificationCountResponse>.Ok(result, "Lấy số lượng thông báo chưa đọc thành công"));
    }

    /// <summary>
    /// Đánh dấu một thông báo thuộc người dùng hiện tại là đã đọc.
    /// </summary>
    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<MarkNotificationReadResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var result = await notificationService.MarkAsReadAsync(notificationId, cancellationToken);
        return Ok(ApiResponse<MarkNotificationReadResponse>.Ok(result, "Đánh dấu đã đọc thông báo thành công"));
    }

    /// <summary>
    /// Đánh dấu toàn bộ thông báo chưa đọc của người dùng hiện tại là đã đọc.
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ApiResponse<MarkAllNotificationsReadResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await notificationService.MarkAllAsReadAsync(cancellationToken);
        return Ok(ApiResponse<MarkAllNotificationsReadResponse>.Ok(result, "Đánh dấu đã đọc tất cả thông báo thành công"));
    }
}
