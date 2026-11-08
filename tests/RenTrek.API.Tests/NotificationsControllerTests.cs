using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.API.Controllers;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Notification.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Tests;

public sealed class NotificationsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorize_WithNoRoleRestriction()
    {
        var attribute = Assert.Single(
            typeof(NotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        var authorize = Assert.IsType<AuthorizeAttribute>(attribute);
        Assert.Null(authorize.Roles);
    }

    [Fact]
    public async Task AllActions_ForwardParametersAndWrapResultInApiResponse()
    {
        var fake = new FakeNotificationService();
        var controller = new NotificationsController(fake);
        var notificationId = Guid.NewGuid();

        var listAction = await controller.GetMyNotificationsAsync(unreadOnly: true, page: 2, pageSize: 15);
        var listOk = Assert.IsType<OkObjectResult>(listAction);
        var listEnvelope = Assert.IsType<ApiResponse<PagedResponse<NotificationItemResponse>>>(listOk.Value);
        Assert.Same(fake.PageResult, listEnvelope.Data);
        Assert.True(fake.ReceivedUnreadOnly);
        Assert.Equal(2, fake.ReceivedPage);
        Assert.Equal(15, fake.ReceivedPageSize);

        var countAction = await controller.GetUnreadCountAsync();
        var countOk = Assert.IsType<OkObjectResult>(countAction);
        var countEnvelope = Assert.IsType<ApiResponse<UnreadNotificationCountResponse>>(countOk.Value);
        Assert.Same(fake.CountResult, countEnvelope.Data);

        var readAction = await controller.MarkAsReadAsync(notificationId);
        var readOk = Assert.IsType<OkObjectResult>(readAction);
        var readEnvelope = Assert.IsType<ApiResponse<MarkNotificationReadResponse>>(readOk.Value);
        Assert.Same(fake.ReadResult, readEnvelope.Data);
        Assert.Equal(notificationId, fake.ReceivedNotificationId);

        var readAllAction = await controller.MarkAllAsReadAsync();
        var readAllOk = Assert.IsType<OkObjectResult>(readAllAction);
        var readAllEnvelope = Assert.IsType<ApiResponse<MarkAllNotificationsReadResponse>>(readAllOk.Value);
        Assert.Same(fake.ReadAllResult, readAllEnvelope.Data);
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public bool? ReceivedUnreadOnly { get; private set; }
        public int ReceivedPage { get; private set; }
        public int ReceivedPageSize { get; private set; }
        public Guid ReceivedNotificationId { get; private set; }

        public PagedResponse<NotificationItemResponse> PageResult { get; } = new() { Items = [], PageNumber = 2, PageSize = 15, TotalItems = 0 };
        public UnreadNotificationCountResponse CountResult { get; } = new() { UnreadCount = 3 };
        public MarkNotificationReadResponse ReadResult { get; } = new() { IsRead = true };
        public MarkAllNotificationsReadResponse ReadAllResult { get; } = new() { MarkedCount = 4 };

        public Task QueueAsync(Guid recipientUserId, string type, string title, string content, string relatedEntityType, Guid relatedEntityId, string eventKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResponse<NotificationItemResponse>> GetMyNotificationsAsync(bool? unreadOnly, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        {
            ReceivedUnreadOnly = unreadOnly;
            ReceivedPage = page;
            ReceivedPageSize = pageSize;
            return Task.FromResult(PageResult);
        }

        public Task<UnreadNotificationCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CountResult);

        public Task<MarkNotificationReadResponse> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            ReceivedNotificationId = notificationId;
            return Task.FromResult(ReadResult);
        }

        public Task<MarkAllNotificationsReadResponse> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ReadAllResult);
    }
}
