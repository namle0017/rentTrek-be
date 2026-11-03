using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.API.Controllers;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
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
        var controller = new TripBookingsController(service);
        var request = new CreateTripBookingRequest { TripId = Guid.NewGuid() };

        var action = await controller.Create(request, "request-key", CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var response = Assert.IsType<ApiResponse<TripBookingResponse>>(result.Value);
        Assert.True(response.Success);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Same(service.CreateResult, response.Data);
        Assert.Equal("request-key", service.ReceivedIdempotencyKey);
    }

    [Fact]
    public async Task GenerateInvitations_ReturnsWrappedTokens()
    {
        var service = new FakeTripBookingService();
        var controller = new TripBookingsController(service);
        var orderId = Guid.NewGuid();

        var action = await controller.GenerateParticipantInvitations(orderId, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<ApiResponse<GroupInvitationResponse>>(result.Value);
        Assert.True(response.Success);
        Assert.Equal("123456", response.Data!.Code);
        Assert.Equal(orderId, service.ReceivedOrderId);
    }

    private sealed class FakeTripBookingService : ITripBookingService
    {
        public TripBookingResponse CreateResult { get; } = new() { OrderId = Guid.NewGuid() };
        public string? ReceivedIdempotencyKey { get; private set; }
        public Guid ReceivedOrderId { get; private set; }

        public Task<TripBookingResponse> ClaimParticipantInvitationAsync(
            Guid orderId,
            string code,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateResult);

        public Task VerifyParticipantInvitationCodeAsync(
            Guid orderId,
            string code,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<InvitationPreviewResponse> GetInvitationPreviewAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new InvitationPreviewResponse { OrderId = orderId });

        public Task<TripBookingResponse> CreateAsync(
            CreateTripBookingRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            ReceivedIdempotencyKey = idempotencyKey;
            return Task.FromResult(CreateResult);
        }

        public Task<GroupInvitationResponse> GenerateParticipantInvitationsAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrderId = orderId;
            return Task.FromResult(new GroupInvitationResponse { Code = "123456" });
        }

        public Task<TripBookingResponse> GetByIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateResult);

        public Task<CancellationPreviewResponse> GetCancellationPreviewAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CancellationPreviewResponse { CanCancel = true });

        public Task<TripBookingResponse> CancelAsync(
            Guid orderId,
            CancelTripBookingRequest? request = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateResult);
    }
}
