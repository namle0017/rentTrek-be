using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using RenTrek.API.Controllers;

namespace RenTrek.API.Tests;

public sealed class RoutingTests
{
    [Fact]
    public async Task AllControllersHaveDistinctHttpMethodAndRouteShape()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(TripBookingsController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();

        var descriptions = app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items).ToList();
        Assert.NotEmpty(descriptions);
        var conflicts = descriptions.GroupBy(description =>
                $"{description.HttpMethod} {Regex.Replace(description.RelativePath!, @"\{[^}]+\}", "{parameter}")}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(description => description.ActionDescriptor.DisplayName))}")
            .ToArray();

        Assert.True(conflicts.Length == 0, string.Join(Environment.NewLine, conflicts));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/admin/posts"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(AdminPostsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/admin/rental-orders"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(AdminRentalOrdersController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/admin/rental-orders/handover-list"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(AdminRentalOrdersController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/admin/rental-orders/return-list"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(AdminRentalOrdersController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/users/me/participations"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(UsersController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/participants/{participantId}/posts"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(ParticipantsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "POST" && description.RelativePath == "api/v1/participants/{participantId}/posts/{postId}/read"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(ParticipantsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "POST" && description.RelativePath == "api/v1/participants/{participantId}/review"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(ParticipantsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "PUT" && description.RelativePath == "api/v1/participants/{participantId}/review"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(ParticipantsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/tours/{tourId}/reviews"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(ToursController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/users/me/notifications"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(NotificationsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/users/me/notifications/unread-count"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(NotificationsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "POST" && description.RelativePath == "api/v1/users/me/notifications/{notificationId}/read"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(NotificationsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "POST" && description.RelativePath == "api/v1/users/me/notifications/read-all"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(NotificationsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "POST" && description.RelativePath == "api/v1/rental-orders/{orderId}/extensions/quote"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(RentalOrdersController));
        Assert.Contains(descriptions, description => description.HttpMethod == "POST" && description.RelativePath == "api/v1/rental-orders/{orderId}/extensions"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(RentalOrdersController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/rental-orders/{orderId}/adjustments"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(RentalOrdersController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/admin/rental-orders/{orderId}/adjustments"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(AdminRentalOrdersController));
    }

    [Theory]
    [InlineData(nameof(RentalOrdersController.QuoteExtensionAsync))]
    [InlineData(nameof(RentalOrdersController.ExtendAsync))]
    [InlineData(nameof(RentalOrdersController.GetAdjustmentsAsync))]
    public void RentalExtensionEndpoints_RequireCustomerOwner(string actionName)
    {
        var authorize = Assert.Single(typeof(RentalOrdersController).GetMethod(actionName)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal(RenTrek.BLL.Constants.RoleNames.Customer, authorize.Roles);
    }
}
