using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.API.Controllers;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Guide.Requests;
using RenTrek.BLL.DTOs.Guide.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Tests;

public sealed class TripOperationsAndGuideControllersTests
{
    [Fact]
    public void Controllers_EnforceExpectedRoleAuthorizationAttributes()
    {
        var recurrenceAuth = Assert.Single(
            typeof(AdminRecurrenceRulesController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin,Staff", ((AuthorizeAttribute)recurrenceAuth).Roles);

        var tripsAuth = Assert.Single(
            typeof(AdminTripsController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin,Staff", ((AuthorizeAttribute)tripsAuth).Roles);

        var groupsAuth = Assert.Single(
            typeof(AdminTripGroupsController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin,Staff", ((AuthorizeAttribute)groupsAuth).Roles);

        var attendancesAuth = Assert.Single(
            typeof(AdminTripAttendancesController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin,Staff,Guide", ((AuthorizeAttribute)attendancesAuth).Roles);

        var guideAuth = Assert.Single(
            typeof(GuideController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Guide,Admin", ((AuthorizeAttribute)guideAuth).Roles);
    }

    [Fact]
    public async Task AdminTripsController_ManifestCsvFormat_ReturnsFileContentResult()
    {
        var controller = new AdminTripsController(new StubAdminTripService());
        var tripId = Guid.NewGuid();

        var action = await controller.GetManifest(tripId, "csv", CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(action);
        Assert.Equal("text/csv; charset=utf-8", fileResult.ContentType);
        Assert.Equal($"manifest-{tripId:N}.csv", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task GuideController_SubmitBatchAttendance_ReturnsStandardApiResponse()
    {
        var controller = new GuideController(new StubGuidePortalService());
        var tripId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var action = await controller.SubmitBatchAttendance(
            tripId,
            new GuideBatchAttendanceRequest
            {
                ActivityId = activityId,
                MarkAllPresent = true
            },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<ApiResponse<GuideBatchAttendanceResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(activityId, response.Data!.ActivityId);
        Assert.Equal("18/18 có mặt", response.Data.ProgressText);
    }

    private sealed class StubAdminTripService : IAdminTripService
    {
        public Task<PagedResponse<AdminTripItemResponse>> GetPagedAsync(AdminTripQueryRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResponse<AdminTripItemResponse>());
        public Task<AdminTripDetailResponse> CreateAsync(CreateAdminTripRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminTripDetailResponse());
        public Task<AdminTripDetailResponse> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminTripDetailResponse { TripId = tripId });
        public Task<AdminTripDetailResponse> UpdateAsync(Guid tripId, UpdateAdminTripRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminTripDetailResponse { TripId = tripId });
        public Task<TripReadinessChecklistResponse> GetReadinessChecklistAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TripReadinessChecklistResponse { IsReadyToDepart = true });
        public Task<AdminTripDetailResponse> OpenBookingAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminTripDetailResponse { TripId = tripId, Status = "Open" });
        public Task<AdminTripDetailResponse> CloseBookingAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminTripDetailResponse { TripId = tripId, Status = "ClosedForBooking" });
        public Task<AdminTripDetailResponse> StartTripAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminTripDetailResponse { TripId = tripId, Status = "InProgress" });
        public Task<AdminTripDetailResponse> CompleteTripAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminTripDetailResponse { TripId = tripId, Status = "Completed" });
        public Task<CancelAdminTripResponse> CancelTripAsync(Guid tripId, CancelAdminTripRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CancelAdminTripResponse { TripId = tripId });
        public Task<IReadOnlyList<AdminTripBookingItemResponse>> GetTripBookingsAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminTripBookingItemResponse>>([]);
        public Task<TripManifestResponse> GetManifestAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TripManifestResponse { TripId = tripId });
        public Task<(byte[] FileBytes, string FileName)> ExportManifestCsvAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult((Encoding.UTF8.GetBytes("STT,HoTen\n1,Test"), $"manifest-{tripId:N}.csv"));
    }

    private sealed class StubGuidePortalService : IGuidePortalService
    {
        public Task<IReadOnlyList<GuideTripCardResponse>> GetMyTripsAsync(string? tab = "today", CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GuideTripCardResponse>>([]);
        public Task<GuideGroupRosterResponse> GetGroupRosterAsync(Guid tripId, string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new GuideGroupRosterResponse { TripId = tripId });
        public Task<GuideParticipantDetailResponse> GetParticipantDetailAsync(Guid participantId, CancellationToken cancellationToken = default)
            => Task.FromResult(new GuideParticipantDetailResponse { ParticipantId = participantId });
        public Task<GuideBatchAttendanceResponse> SubmitBatchAttendanceAsync(Guid tripId, GuideBatchAttendanceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new GuideBatchAttendanceResponse
            {
                ActivityId = request.ActivityId,
                UpdatedCount = 18,
                PresentCount = 18,
                TotalCount = 18,
                ProgressText = "18/18 có mặt"
            });
        public Task<GuideTripItineraryResponse> GetTripItineraryAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new GuideTripItineraryResponse { TripId = tripId });
        public Task<GuideTripProvidedItemsResponse> GetProvidedItemsAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(new GuideTripProvidedItemsResponse { TripId = tripId });
    }
}
