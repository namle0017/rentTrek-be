using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.API.Controllers;
using RenTrek.BLL.DTOs.Order.Requests;
using RenTrek.BLL.DTOs.Order.Responses;
using RenTrek.BLL.DTOs.Participation.Responses;
using RenTrek.BLL.DTOs.Review.Requests;
using RenTrek.BLL.DTOs.Review.Responses;
using RenTrek.BLL.DTOs.Tour.Requests;
using RenTrek.BLL.DTOs.Tour.Responses;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.DTOs.TripGroup.Responses;
using RenTrek.BLL.DTOs.Users.Requests;
using RenTrek.BLL.DTOs.Users.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Tests;

public sealed class ParticipantsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedUser_AndNoRoleRestriction()
    {
        var attribute = Assert.Single(
            typeof(ParticipantsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        var authorize = Assert.IsType<AuthorizeAttribute>(attribute);
        Assert.Null(authorize.Roles);
    }

    [Fact]
    public async Task GetPosts_And_MarkPostAsRead_ReturnOkApiResponse()
    {
        var participantId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var service = new FakeParticipationService();
        var controller = new ParticipantsController(service, new FakeReviewService());

        var postsAction = await controller.GetPostsAsync(participantId, CancellationToken.None);
        var postsResult = Assert.IsType<OkObjectResult>(postsAction);
        var postsEnvelope = Assert.IsType<ApiResponse<IReadOnlyList<ParticipantPostResponse>>>(postsResult.Value);
        Assert.True(postsEnvelope.Success);
        Assert.Same(service.PostsResult, postsEnvelope.Data);
        Assert.Equal(participantId, service.ReceivedParticipantId);

        var readAction = await controller.MarkPostAsReadAsync(participantId, postId, CancellationToken.None);
        var readResult = Assert.IsType<OkObjectResult>(readAction);
        var readEnvelope = Assert.IsType<ApiResponse<ParticipantPostReadResponse>>(readResult.Value);
        Assert.True(readEnvelope.Success);
        Assert.Same(service.ReadResult, readEnvelope.Data);
        Assert.Equal(postId, service.ReceivedPostId);
    }

    [Fact]
    public async Task CreateReview_And_UpdateReview_ReturnExpectedStatusAndEnvelope()
    {
        var participantId = Guid.NewGuid();
        var reviewService = new FakeReviewService();
        var controller = new ParticipantsController(new FakeParticipationService(), reviewService);
        var request = new SaveReviewRequest { Rating = 5, Comment = "Xuất sắc" };

        var createAction = await controller.CreateReviewAsync(participantId, request, CancellationToken.None);
        var createResult = Assert.IsType<ObjectResult>(createAction);
        Assert.Equal(StatusCodes.Status201Created, createResult.StatusCode);
        var createEnvelope = Assert.IsType<ApiResponse<ParticipantReviewResponse>>(createResult.Value);
        Assert.Same(reviewService.ReviewResult, createEnvelope.Data);
        Assert.Equal(participantId, reviewService.ReceivedParticipantId);

        var updateAction = await controller.UpdateReviewAsync(participantId, request, CancellationToken.None);
        var updateResult = Assert.IsType<OkObjectResult>(updateAction);
        var updateEnvelope = Assert.IsType<ApiResponse<ParticipantReviewResponse>>(updateResult.Value);
        Assert.Same(reviewService.ReviewResult, updateEnvelope.Data);
    }

    [Fact]
    public async Task ToursController_GetReviews_AllowsAnonymous_AndReturnsOkApiResponse()
    {
        var method = typeof(ToursController).GetMethod(nameof(ToursController.GetReviewsAsync))!;
        Assert.NotEmpty(method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        Assert.Empty(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        var tourId = Guid.NewGuid();
        var reviewService = new FakeReviewService();
        var controller = new ToursController(new UnusedTourService(), new UnusedRecurrenceService(), reviewService);

        var action = await controller.GetReviewsAsync(tourId, page: 2, pageSize: 5, CancellationToken.None);
        var result = Assert.IsType<OkObjectResult>(action);
        var envelope = Assert.IsType<ApiResponse<TourReviewsResponse>>(result.Value);
        Assert.Same(reviewService.TourReviewsResult, envelope.Data);
        Assert.Equal(tourId, reviewService.ReceivedTourId);
        Assert.Equal(2, reviewService.ReceivedPage);
        Assert.Equal(5, reviewService.ReceivedPageSize);
    }

    [Fact]
    public async Task UsersController_GetMyParticipations_RequiresAuthorize_AndReturnsOkApiResponse()
    {
        var auth = Assert.Single(
            typeof(UsersController).GetMethod(nameof(UsersController.GetMyParticipationsAsync))!
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Null(auth.Roles);

        var service = new FakeParticipationService();
        var controller = new UsersController(
            new UnusedUserService(),
            new UnusedOrderService(),
            service,
            new FixedCurrentUser());

        var action = await controller.GetMyParticipationsAsync(CancellationToken.None);
        var result = Assert.IsType<OkObjectResult>(action);
        var envelope = Assert.IsType<ApiResponse<IReadOnlyList<MyParticipationResponse>>>(result.Value);
        Assert.True(envelope.Success);
        Assert.Same(service.ParticipationsResult, envelope.Data);
    }

    private sealed class FakeParticipationService : IParticipationService
    {
        public Guid ReceivedParticipantId { get; private set; }
        public Guid ReceivedPostId { get; private set; }
        public IReadOnlyList<MyParticipationResponse> ParticipationsResult { get; } = [];
        public IReadOnlyList<ParticipantPostResponse> PostsResult { get; } = [];
        public ParticipantPostReadResponse ReadResult { get; } = new() { IsRead = true };

        public Task<IReadOnlyList<MyParticipationResponse>> GetMyParticipationsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ParticipationsResult);

        public Task<IReadOnlyList<ParticipantPostResponse>> GetParticipantPostsAsync(
            Guid participantId,
            CancellationToken cancellationToken = default)
        {
            ReceivedParticipantId = participantId;
            return Task.FromResult(PostsResult);
        }

        public Task<ParticipantPostReadResponse> MarkPostAsReadAsync(
            Guid participantId,
            Guid postId,
            CancellationToken cancellationToken = default)
        {
            ReceivedParticipantId = participantId;
            ReceivedPostId = postId;
            return Task.FromResult(ReadResult);
        }
    }

    private sealed class UnusedUserService : IUserService
    {
        public Task<UserDetailResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserDetailResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> UploadAvatarAsync(Guid userId, Stream stream, string fileName, string contentType, long length, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? currentJti, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedOrderService : IOrderService
    {
        public Task<BLL.DTOs.Common.Responses.PagedResponse<MyOrderResponse>> GetMyOrdersAsync(Guid userId, MyOrderQueryRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedCurrentUser : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid RequiredUserId => UserId!.Value;
        public string? Jti => null;
        public string? Role => "Customer";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeReviewService : IReviewService
    {
        public Guid ReceivedParticipantId { get; private set; }
        public Guid ReceivedTourId { get; private set; }
        public int ReceivedPage { get; private set; }
        public int ReceivedPageSize { get; private set; }
        public ParticipantReviewResponse ReviewResult { get; } = new() { Rating = 5 };
        public TourReviewsResponse TourReviewsResult { get; } = new() { ReviewCount = 1, AverageRating = 5m };

        public Task<ParticipantReviewResponse> CreateReviewAsync(
            Guid participantId,
            SaveReviewRequest request,
            CancellationToken cancellationToken = default)
        {
            ReceivedParticipantId = participantId;
            return Task.FromResult(ReviewResult);
        }

        public Task<ParticipantReviewResponse> UpdateReviewAsync(
            Guid participantId,
            SaveReviewRequest request,
            CancellationToken cancellationToken = default)
        {
            ReceivedParticipantId = participantId;
            return Task.FromResult(ReviewResult);
        }

        public Task<TourReviewsResponse> GetTourReviewsAsync(
            Guid tourId,
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            ReceivedTourId = tourId;
            ReceivedPage = page;
            ReceivedPageSize = pageSize;
            return Task.FromResult(TourReviewsResult);
        }
    }

    private sealed class UnusedTourService : ITourService
    {
        public Task<BLL.DTOs.Common.Responses.PagedResponse<BLL.DTOs.AdminCatalog.Responses.AdminTourListItemResponse>> GetAdminToursAsync(BLL.DTOs.AdminCatalog.Requests.AdminTourQueryRequest query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BLL.DTOs.Common.Responses.PagedResponse<TourResponse>> GetToursAsync(TourQueryRequest query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TourDetailResponse> GetTourByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedRecurrenceService : ITripRecurrenceService
    {
        public Task<TourGroupSettingsResponse> UpdateTourGroupSettingsAsync(Guid tourId, UpdateTourGroupSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TourRecurrenceRuleResponse> CreateRecurrenceRuleAsync(Guid tourId, CreateTourRecurrenceRuleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TourRecurrenceRuleResponse>> GetRecurrenceRulesAsync(Guid tourId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GenerateRecurringTripsResponse> GenerateRecurringTripsAsync(GenerateRecurringTripsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
