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
            typeof(TripGroupsController).GetMethod(nameof(TripGroupsController.GetGroupingSnapshot))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.Equal(
            $"{RoleNames.Staff},{RoleNames.Admin},{RoleNames.Guide},{RoleNames.TourGuide}",
            Assert.IsType<AuthorizeAttribute>(snapshotAuth).Roles);

        foreach (var mutationName in new[]
        {
            nameof(TripGroupsController.AssignGuide),
            nameof(TripGroupsController.MoveBooking),
            nameof(TripGroupsController.SplitBooking),
            nameof(TripGroupsController.RecalculateProvidedItems),
            nameof(TripGroupsController.Publish)
        })
        {
            var auth = Assert.Single(
                typeof(TripGroupsController).GetMethod(mutationName)!
                    .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
            Assert.Equal($"{RoleNames.Staff},{RoleNames.Admin}", Assert.IsType<AuthorizeAttribute>(auth).Roles);
        }
    }

    [Fact]
    public void GuideTripGroupsController_RequiresGuideOrTourGuideRole()
    {
        var attribute = Assert.Single(
            typeof(GuideTripGroupsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        var authorize = Assert.IsType<AuthorizeAttribute>(attribute);
        Assert.Equal($"{RoleNames.Guide},{RoleNames.TourGuide}", authorize.Roles);
    }

    [Fact]
    public void TripsController_CreateAndUpdate_RequireAdminRole()
    {
        var createAuth = Assert.Single(
            typeof(TripsController).GetMethod(nameof(TripsController.Create))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        var updateAuth = Assert.Single(
            typeof(TripsController).GetMethod(nameof(TripsController.Update))!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        Assert.Equal(RoleNames.Admin, Assert.IsType<AuthorizeAttribute>(createAuth).Roles);
        Assert.Equal(RoleNames.Admin, Assert.IsType<AuthorizeAttribute>(updateAuth).Roles);
    }
}
