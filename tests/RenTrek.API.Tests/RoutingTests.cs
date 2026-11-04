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
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/admin/management/posts"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(RenTrek.API.Controllers.Admin.AdminPostsController));
        Assert.Contains(descriptions, description => description.HttpMethod == "GET" && description.RelativePath == "api/v1/admin/management/rental-orders"
            && ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo == typeof(RenTrek.API.Controllers.Admin.AdminRentalOrdersController));
    }
}
