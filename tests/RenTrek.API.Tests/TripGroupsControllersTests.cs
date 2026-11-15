using Microsoft.AspNetCore.Authorization;
using RenTrek.API.Controllers;
using RenTrek.BLL.Constants;

namespace RenTrek.API.Tests;

public sealed class TripGroupsControllersTests
{
    [Fact]
    public void TripGroupsController_EnforcesRoleScopesForSnapshotAndMutations()
    {
        var snapshotAuth = Assert.Single(
            typeof(TripGroupsController).GetMethod(nameof(TripGroupsController.GetGroupingSnapshotAsync))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.Equal(
            $"{RoleNames.Staff},{RoleNames.Admin},{RoleNames.Guide}",
            Assert.IsType<AuthorizeAttribute>(snapshotAuth).Roles);

        foreach (var mutationName in new[]
        {
            nameof(TripGroupsController.AssignGuideAsync),
            nameof(TripGroupsController.MoveBookingAsync),
            nameof(TripGroupsController.SplitBookingAsync),
            nameof(TripGroupsController.RecalculateProvidedItemsAsync),
            nameof(TripGroupsController.PublishAsync)
        })
        {
            var auth = Assert.Single(
                typeof(TripGroupsController).GetMethod(mutationName)!
                    .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
            Assert.Equal($"{RoleNames.Staff},{RoleNames.Admin}", Assert.IsType<AuthorizeAttribute>(auth).Roles);
        }
    }

    [Fact]
    public void GuideTripGroupsController_RequiresGuideRole()
    {
        var attribute = Assert.Single(
            typeof(GuideTripGroupsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        var authorize = Assert.IsType<AuthorizeAttribute>(attribute);
        Assert.Equal(RoleNames.Guide, authorize.Roles);
    }

    [Fact]
    public void TripsController_CreateAndUpdate_RequireAdminRole()
    {
        var createAuth = Assert.Single(
            typeof(TripsController).GetMethod(nameof(TripsController.CreateAsync))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        var updateAuth = Assert.Single(
            typeof(TripsController).GetMethod(nameof(TripsController.UpdateAsync))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        Assert.Equal(RoleNames.Admin, Assert.IsType<AuthorizeAttribute>(createAuth).Roles);
        Assert.Equal(RoleNames.Admin, Assert.IsType<AuthorizeAttribute>(updateAuth).Roles);
    }
}
