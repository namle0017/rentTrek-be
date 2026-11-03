using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.API.Controllers;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Tests;

public sealed class TripBookingsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var attribute = Assert.Single(
            typeof(TripBookingsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        Assert.IsType<AuthorizeAttribute>(attribute);
    }

    [Fact]
    public async Task Create_ReturnsCreatedApiResponseAndForwardsIdempotencyKey()
    {
        var service = new FakeTripBookingService();
        var groupService = new FakeTripGroupService();
        var controller = new TripBookingsController(service, groupService);
        var request = new CreateTripBookingRequest { TripId = Guid.NewGuid(), ContactPhone = "0901234567" };

        var action = await controller.Create(request, "request-key", CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var response = Assert.IsType<ApiResponse<TripBookingResponse>>(result.Value);
        Assert.True(response.Success);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Same(service.BookingResult, response.Data);
        Assert.Equal("request-key", service.ReceivedIdempotencyKey);
    }

    [Fact]
    public async Task UpdateParticipant_ForwardsIdsAndReturnsBooking()
    {
        var service = new FakeTripBookingService();
        var groupService = new FakeTripGroupService();
        var controller = new TripBookingsController(service, groupService);
        var orderId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var request = new UpdateBookingParticipantRequest { FullName = "Nguyễn Văn B", Email = "b@example.com" };

        var action = await controller.UpdateParticipant(orderId, participantId, request, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<ApiResponse<TripBookingResponse>>(result.Value);
        Assert.True(response.Success);
        Assert.Equal(orderId, service.ReceivedOrderId);
        Assert.Equal(participantId, service.ReceivedParticipantId);
        Assert.Same(service.BookingResult, response.Data);
    }

    [Fact]
    public async Task GetInviteLink_ReturnsWrappedLink()
    {
        var service = new FakeTripBookingService();
        var groupService = new FakeTripGroupService();
        var controller = new TripBookingsController(service, groupService);
        var orderId = Guid.NewGuid();

        var action = await controller.GetInviteLink(orderId, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<ApiResponse<InviteLinkResponse>>(result.Value);
        Assert.True(response.Success);
        Assert.Equal("link", response.Data!.Url);
        Assert.Equal(orderId, service.ReceivedOrderId);
    }

    [Fact]
    public async Task Preview_ForwardsTokenAndAllowsAnonymous()
    {
        var service = new FakeTripBookingService();
        var groupService = new FakeTripGroupService();
        var controller = new TripBookingsController(service, groupService);
        var request = new InvitationPreviewRequest { OrderId = Guid.NewGuid(), Token = "token" };

        var action = await controller.GetInvitationPreview(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(action);
        Assert.Equal("token", service.ReceivedToken);
        Assert.NotEmpty(typeof(TripBookingsController)
            .GetMethod(nameof(TripBookingsController.GetInvitationPreview))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    [Fact]
    public async Task GetGrouping_And_Cancel_ForwardToServices()
    {
        var service = new FakeTripBookingService();
        var groupService = new FakeTripGroupService();
        var controller = new TripBookingsController(service, groupService);
        var orderId = Guid.NewGuid();

        var groupingAction = await controller.GetGrouping(orderId, CancellationToken.None);
        var cancelAction = await controller.Cancel(orderId, new CancelTripBookingRequest { Reason = "Bận đột xuất" }, CancellationToken.None);

        var groupingResult = Assert.IsType<OkObjectResult>(groupingAction);
        var groupingResponse = Assert.IsType<ApiResponse<CustomerTripGroupingResponse>>(groupingResult.Value);
        Assert.True(groupingResponse.Success);
        Assert.Equal(orderId, groupService.ReceivedOrderId);

        var cancelResult = Assert.IsType<OkObjectResult>(cancelAction);
        var cancelResponse = Assert.IsType<ApiResponse<TripBookingResponse>>(cancelResult.Value);
        Assert.True(cancelResponse.Success);
        Assert.Equal(orderId, service.ReceivedOrderId);
    }

    private sealed class FakeTripBookingService : ITripBookingService
    {
        public TripBookingResponse BookingResult { get; } = new() { OrderId = Guid.NewGuid() };
        public string? ReceivedIdempotencyKey { get; private set; }
        public Guid ReceivedOrderId { get; private set; }
        public Guid ReceivedParticipantId { get; private set; }
        public string? ReceivedToken { get; private set; }

        public Task<InvitationPreviewResponse> GetInvitationPreviewAsync(
            Guid orderId,
            string token,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = token;
            return Task.FromResult(new InvitationPreviewResponse { OrderId = orderId });
        }

        public Task<TripBookingResponse> CreateAsync(
            CreateTripBookingRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            ReceivedIdempotencyKey = idempotencyKey;
            return Task.FromResult(BookingResult);
        }

        public Task<TripBookingResponse> UpdateParticipantAsync(
            Guid orderId,
            Guid participantId,
            UpdateBookingParticipantRequest request,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            ReceivedParticipantId = participantId;
            return Task.FromResult(BookingResult);
        }

        public Task<CancellationPreviewResponse> GetCancellationPreviewAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CancellationPreviewResponse());

        public Task<TripBookingResponse> CancelAsync(
            Guid orderId,
            CancelTripBookingRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            return Task.FromResult(BookingResult);
        }

        public Task<InviteLinkResponse> GetInviteLinkAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            return Task.FromResult(new InviteLinkResponse { Url = "link" });
        }

        public Task<InviteLinkResponse> RotateInviteLinkAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new InviteLinkResponse { Url = "link" });

        public Task<TripBookingResponse> GetByIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BookingResult);
    }

    private sealed class FakeTripGroupService : ITripGroupService
    {
        public Guid ReceivedOrderId { get; private set; }

        public Task<CustomerTripGroupingResponse> GetCustomerGroupingAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            return Task.FromResult(new CustomerTripGroupingResponse { GroupingStatus = "Pending" });
        }

        public Task<TourGroupSettingsResponse> UpdateTourGroupSettingsAsync(Guid tourId, UpdateTourGroupSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TourRecurrenceRuleResponse> CreateRecurrenceRuleAsync(Guid tourId, CreateTourRecurrenceRuleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TourRecurrenceRuleResponse>> GetRecurrenceRulesAsync(Guid tourId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GenerateRecurringTripsResponse> GenerateRecurringTripsAsync(GenerateRecurringTripsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminTripResponse> GetAdminTripAsync(Guid tripId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminTripResponse> CreateTripAsync(CreateTripRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminTripResponse> UpdateTripAsync(Guid tripId, UpdateTripRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminTripResponse> UpdateTripStatusAsync(Guid tripId, UpdateTripStatusRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AutoAssignPaidBookingInTransactionAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TripGroupingSnapshotResponse> GetGroupingSnapshotAsync(Guid tripId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TripGroupingSnapshotResponse> AssignGuideAsync(Guid tripId, Guid groupId, AssignTripGroupGuideRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TripGroupingSnapshotResponse> MoveBookingAsync(Guid tripId, Guid bookingOrderId, MoveBookingGroupRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TripGroupingSnapshotResponse> SplitBookingAsync(Guid tripId, Guid bookingOrderId, SplitTripBookingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RecalculateTripProvidedItemsResponse> RecalculateProvidedItemsAsync(Guid tripId, PublishTripGroupingRequest? request = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TripGroupingSnapshotResponse> PublishGroupsAsync(Guid tripId, PublishTripGroupingRequest? request = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<GuideTripGroupResponse>> GetGuideGroupsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GuideTripGroupResponse> GetGuideGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
