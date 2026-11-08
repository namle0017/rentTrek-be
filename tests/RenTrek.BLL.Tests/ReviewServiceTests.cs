using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Review.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Validators.Review;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Kiểm thử nghiệp vụ gửi, chỉnh sửa đánh giá chuyến đi và thống kê đánh giá công khai theo họ tour (T2).
/// </summary>
public sealed class ReviewServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateReview_NotOwnedOrDeleted_Throws404()
    {
        var repo = new FakeReviewRepository { ParticipantContext = null };
        var service = CreateService(repo);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 5 }));
    }

    [Fact]
    public async Task CreateReview_TripNotCompleted_Throws409()
    {
        var repo = new FakeReviewRepository
        {
            ParticipantContext = EligibleContext(tripStatus: TripStatuses.InProgress)
        };
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 5 }));
        Assert.Equal(ErrorCodes.ReviewNotEligible, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateReview_OrderUnpaid_Throws409()
    {
        var repo = new FakeReviewRepository
        {
            ParticipantContext = EligibleContext(paymentStatus: OrderPaymentStatuses.Unpaid)
        };
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 4 }));
        Assert.Equal(ErrorCodes.ReviewNotEligible, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateReview_OrderOrParticipantCancelled_Throws409()
    {
        var cancelledOrderRepo = new FakeReviewRepository
        {
            ParticipantContext = EligibleContext(orderStatus: OrderStatuses.Cancelled)
        };
        var exOrder = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(cancelledOrderRepo).CreateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 4 }));
        Assert.Equal(ErrorCodes.ReviewNotEligible, exOrder.ErrorCode);

        var cancelledParticipantRepo = new FakeReviewRepository
        {
            ParticipantContext = EligibleContext(participantStatus: BookingParticipantStatuses.Cancelled)
        };
        var exParticipant = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(cancelledParticipantRepo).CreateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 4 }));
        Assert.Equal(ErrorCodes.ReviewNotEligible, exParticipant.ErrorCode);
    }

    [Fact]
    public async Task CreateReview_AlreadyReviewed_Throws409ReviewAlreadyExists()
    {
        var repo = new FakeReviewRepository
        {
            ParticipantContext = EligibleContext(hasReview: true)
        };
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 5 }));
        Assert.Equal(ErrorCodes.ReviewAlreadyExists, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateReview_ConcurrentUniqueViolation_Throws409ReviewAlreadyExists()
    {
        var repo = new FakeReviewRepository { ParticipantContext = EligibleContext() };
        var uow = new FakeReviewUnitOfWork(repo, new FakeSystemConfigRepository()) { TrySaveResult = false };
        var service = new ReviewService(uow, new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 5 }));
        Assert.Equal(ErrorCodes.ReviewAlreadyExists, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateReview_EligibleAndPartiallyRefunded_CreatesTrimmedReview()
    {
        var participantId = Guid.NewGuid();
        var repo = new FakeReviewRepository
        {
            ParticipantContext = EligibleContext(participantId: participantId, paymentStatus: OrderPaymentStatuses.PartiallyRefunded)
        };
        var service = CreateService(repo);

        var result = await service.CreateReviewAsync(participantId, new SaveReviewRequest
        {
            Rating = 5,
            Comment = "  Hướng dẫn viên rất nhiệt tình!  "
        });

        Assert.Equal(participantId, result.ParticipantId);
        Assert.Equal(5, result.Rating);
        Assert.Equal("Hướng dẫn viên rất nhiệt tình!", result.Comment);
        Assert.Equal(FixedNow.UtcDateTime, result.CreatedAt);
        Assert.Equal(FixedNow.UtcDateTime, result.UpdatedAt);
        Assert.NotNull(repo.AddedReview);
        Assert.False(repo.AddedReview!.IsHidden);
    }

    [Theory]
    [InlineData(0, "Tốt", false)]
    [InlineData(6, null, false)]
    [InlineData(1, null, true)]
    [InlineData(5, "Rất tốt", true)]
    public void SaveReviewValidator_RatingAndCommentRules_BehaveAsExpected(int rating, string? comment, bool expectedValid)
    {
        var validator = new SaveReviewRequestValidator();
        var result = validator.Validate(new SaveReviewRequest { Rating = rating, Comment = comment });
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void SaveReviewValidator_CommentOver2000Chars_Fails()
    {
        var validator = new SaveReviewRequestValidator();
        var result = validator.Validate(new SaveReviewRequest { Rating = 5, Comment = new string('a', 2001) });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveReviewRequest.Comment));
    }

    [Fact]
    public async Task UpdateReview_NotOwnedOrMissingReview_Throws404()
    {
        var repo = new FakeReviewRepository { ParticipantContext = null };
        var service = CreateService(repo);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 4 }));

        var noReviewRepo = new FakeReviewRepository { ParticipantContext = EligibleContext(), ExistingReview = null };
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(noReviewRepo).UpdateReviewAsync(Guid.NewGuid(), new SaveReviewRequest { Rating = 4 }));
    }

    [Fact]
    public async Task UpdateReview_AfterConfiguredWindow_Throws409EditWindowClosed()
    {
        var participantId = Guid.NewGuid();
        var existing = new Review
        {
            ReviewId = Guid.NewGuid(),
            ParticipantId = participantId,
            Rating = 4,
            Comment = "Cũ",
            CreatedAt = FixedNow.UtcDateTime.AddDays(-3).AddSeconds(-1),
            UpdatedAt = FixedNow.UtcDateTime.AddDays(-3).AddSeconds(-1)
        };
        var repo = new FakeReviewRepository { ParticipantContext = EligibleContext(participantId), ExistingReview = existing };
        var configRepo = new FakeSystemConfigRepository(new() { [ReviewConfigKeys.ReviewEditWindowDays] = "3" });
        var uow = new FakeReviewUnitOfWork(repo, configRepo);
        var service = new ReviewService(uow, new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateReviewAsync(participantId, new SaveReviewRequest { Rating = 5, Comment = "Sửa" }));
        Assert.Equal(ErrorCodes.ReviewEditWindowClosed, ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateReview_HiddenReviewWithinWindow_UpdatesContentPreservesHiddenFlag()
    {
        var participantId = Guid.NewGuid();
        var existing = new Review
        {
            ReviewId = Guid.NewGuid(),
            ParticipantId = participantId,
            Rating = 2,
            Comment = "Bị ẩn",
            IsHidden = true,
            HiddenReason = "Vi phạm từ ngữ",
            CreatedAt = FixedNow.UtcDateTime.AddDays(-6),
            UpdatedAt = FixedNow.UtcDateTime.AddDays(-6)
        };
        var repo = new FakeReviewRepository { ParticipantContext = EligibleContext(participantId), ExistingReview = existing };
        var configRepo = new FakeSystemConfigRepository();
        var uow = new FakeReviewUnitOfWork(repo, configRepo);
        var service = new ReviewService(uow, new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

        var result = await service.UpdateReviewAsync(participantId, new SaveReviewRequest { Rating = 4, Comment = "Đã sửa lại" });

        Assert.Equal(4, result.Rating);
        Assert.Equal("Đã sửa lại", result.Comment);
        Assert.Equal(FixedNow.UtcDateTime, result.UpdatedAt);
        Assert.True(existing.IsHidden);
        Assert.Equal("Vi phạm từ ngữ", existing.HiddenReason);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task GetTourReviews_MissingOrDraftTour_Throws404()
    {
        var repo = new FakeReviewRepository { PublicTourFamilyId = null };
        var service = CreateService(repo);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetTourReviewsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTourReviews_AggregatesAverageDistributionAndPaging()
    {
        var familyId = Guid.NewGuid();
        var repo = new FakeReviewRepository
        {
            PublicTourFamilyId = familyId,
            TourReviewsSummary = new TourReviewsSummaryRow
            {
                RatingCounts = new Dictionary<int, int> { [5] = 2, [4] = 1, [3] = 1 },
                TotalCount = 4,
                Items =
                [
                    new TourReviewRow
                    {
                        ReviewId = Guid.NewGuid(),
                        Rating = 5,
                        Comment = "Tuyệt",
                        CreatedAt = FixedNow.UtcDateTime,
                        ReviewerName = "Nguyễn Văn A",
                        TripStartAt = FixedNow.AddDays(-10)
                    }
                ]
            }
        };
        var service = CreateService(repo);

        var result = await service.GetTourReviewsAsync(Guid.NewGuid(), page: 0, pageSize: 500);

        Assert.Equal(4.3m, result.AverageRating); // (5*2 + 4 + 3) / 4 = 4.25 -> 4.3
        Assert.Equal(4, result.ReviewCount);
        Assert.Equal(0, result.Distribution[1]);
        Assert.Equal(0, result.Distribution[2]);
        Assert.Equal(1, result.Distribution[3]);
        Assert.Equal(1, result.Distribution[4]);
        Assert.Equal(2, result.Distribution[5]);
        Assert.Equal(1, result.Reviews.PageNumber);
        Assert.Equal(100, result.Reviews.PageSize);
        Assert.Single(result.Reviews.Items);
        Assert.Equal("Nguyễn Văn A", result.Reviews.Items[0].ReviewerName);
    }

    [AdminPostgresFact]
    public async Task TourReviews_Postgres_ExcludesHiddenAggregatesFamilyAndBlocksDuplicates()
    {
        await using var db = new AdminTestDatabase();
        await db.InitializeAsync();
        var t0 = db.Now;
        var familyA = Guid.NewGuid();
        var familyB = Guid.NewGuid();
        var tourA1 = Guid.NewGuid();
        var tourA2 = Guid.NewGuid();
        var tourDraft = Guid.NewGuid();
        var tourB = Guid.NewGuid();
        var tripA1 = Guid.NewGuid();
        var tripA2 = Guid.NewGuid();
        var tripB = Guid.NewGuid();
        var pA1 = Guid.NewGuid();
        var pA2Hidden = Guid.NewGuid();
        var pA3 = Guid.NewGuid();
        var pB = Guid.NewGuid();

        await using (var setup = db.Context())
        {
            var category = await setup.Categories.FirstAsync();
            var location = await setup.Locations.FirstAsync();
            var policyId = db.PolicyId;

            setup.Tours.AddRange(
                new Tour { Id = tourA1, TourFamilyId = familyA, Version = 1, Name = "Family A v1", CategoryId = category.Id, MainLocationId = location.Id, CreatedByUserId = db.AdminId, Status = TourStatuses.Archived },
                new Tour { Id = tourA2, TourFamilyId = familyA, Version = 2, Name = "Family A v2", CategoryId = category.Id, MainLocationId = location.Id, CreatedByUserId = db.AdminId, Status = TourStatuses.Published },
                new Tour { Id = tourDraft, TourFamilyId = familyA, Version = 3, Name = "Family A v3", CategoryId = category.Id, MainLocationId = location.Id, CreatedByUserId = db.AdminId, Status = TourStatuses.Draft },
                new Tour { Id = tourB, TourFamilyId = familyB, Version = 1, Name = "Family B", CategoryId = category.Id, MainLocationId = location.Id, CreatedByUserId = db.AdminId, Status = TourStatuses.Published });

            setup.Trips.AddRange(
                new Trip { TripId = tripA1, TourId = tourA1, RefundPolicyId = policyId, ManagedByUserId = db.StaffId, StartAt = t0.AddDays(-20), EndAt = t0.AddDays(-18), TotalSlots = 10, MinParticipants = 1, Status = TripStatuses.Completed, CreatedAt = t0, UpdatedAt = t0 },
                new Trip { TripId = tripA2, TourId = tourA2, RefundPolicyId = policyId, ManagedByUserId = db.StaffId, StartAt = t0.AddDays(-10), EndAt = t0.AddDays(-8), TotalSlots = 10, MinParticipants = 1, Status = TripStatuses.Completed, CreatedAt = t0, UpdatedAt = t0 },
                new Trip { TripId = tripB, TourId = tourB, RefundPolicyId = policyId, ManagedByUserId = db.StaffId, StartAt = t0.AddDays(-10), EndAt = t0.AddDays(-8), TotalSlots = 10, MinParticipants = 1, Status = TripStatuses.Completed, CreatedAt = t0, UpdatedAt = t0 });

            var orderA = NewPaidTripOrder(db.CustomerId, tripA1, "REV-A1", policyId, t0);
            var orderA2 = NewPaidTripOrder(db.CustomerId, tripA2, "REV-A2", policyId, t0);
            var orderB = NewPaidTripOrder(db.CustomerId, tripB, "REV-B1", policyId, t0);
            setup.Orders.AddRange(orderA, orderA2, orderB);

            setup.BookingParticipants.AddRange(
                new BookingParticipant { ParticipantId = pA1, BookingOrderId = orderA.OrderId, LinkedUserId = db.CustomerId, TripId = tripA1, FullName = "Khách V1", Status = BookingParticipantStatuses.Completed, CreatedAt = t0, UpdatedAt = t0 },
                new BookingParticipant { ParticipantId = pA2Hidden, BookingOrderId = orderA2.OrderId, LinkedUserId = db.OtherStaffId, TripId = tripA2, FullName = "Khách Bị Ẩn", Status = BookingParticipantStatuses.Completed, CreatedAt = t0, UpdatedAt = t0 },
                new BookingParticipant { ParticipantId = pA3, BookingOrderId = orderA2.OrderId, LinkedUserId = db.StaffId, TripId = tripA2, FullName = "Khách V2", Status = BookingParticipantStatuses.Completed, CreatedAt = t0, UpdatedAt = t0 },
                new BookingParticipant { ParticipantId = pB, BookingOrderId = orderB.OrderId, LinkedUserId = db.CustomerId, TripId = tripB, FullName = "Khách Họ B", Status = BookingParticipantStatuses.Completed, CreatedAt = t0, UpdatedAt = t0 });

            setup.Reviews.AddRange(
                new Review { ReviewId = Guid.NewGuid(), ParticipantId = pA2Hidden, Rating = 1, Comment = "Spam", IsHidden = true, HiddenReason = "Spam", CreatedAt = t0.UtcDateTime.AddHours(-2), UpdatedAt = t0.UtcDateTime },
                new Review { ReviewId = Guid.NewGuid(), ParticipantId = pA3, Rating = 4, Comment = "Tốt", IsHidden = false, CreatedAt = t0.UtcDateTime.AddHours(-1), UpdatedAt = t0.UtcDateTime },
                new Review { ReviewId = Guid.NewGuid(), ParticipantId = pB, Rating = 2, Comment = "Họ B", IsHidden = false, CreatedAt = t0.UtcDateTime, UpdatedAt = t0.UtcDateTime });

            await setup.SaveChangesAsync();
        }

        // Customer tạo review lần 1 thành công, lần 2 trùng trả 409, DB vẫn 1 dòng
        await using (var ctx = db.Context())
        {
            var svc = new ReviewService(new UnitOfWork(ctx), new FixedCurrentUser(db.CustomerId), new FixedClock(t0));
            var created = await svc.CreateReviewAsync(pA1, new SaveReviewRequest { Rating = 5, Comment = "Quá tuyệt" });
            Assert.Equal(5, created.Rating);

            var dupEx = await Assert.ThrowsAsync<ConflictException>(() =>
                svc.CreateReviewAsync(pA1, new SaveReviewRequest { Rating = 3, Comment = "Trùng" }));
            Assert.Equal(ErrorCodes.ReviewAlreadyExists, dupEx.ErrorCode);
            Assert.Equal(1, await ctx.Reviews.CountAsync(r => r.ParticipantId == pA1));
        }

        // Review công khai cho tourA2 (và tourA1 archived): gộp A1 + A2 (5 và 4 => 4.5), loại bỏ hidden (1) và không lẫn tourB (2); Draft -> 404
        await using (var readCtx = db.Context())
        {
            var svc = new ReviewService(new UnitOfWork(readCtx), new FixedCurrentUser(db.CustomerId), new FixedClock(t0));
            var summaryA = await svc.GetTourReviewsAsync(tourA2, 1, 10);
            Assert.Equal(2, summaryA.ReviewCount);
            Assert.Equal(4.5m, summaryA.AverageRating);
            Assert.Equal(0, summaryA.Distribution[1]);
            Assert.Equal(1, summaryA.Distribution[4]);
            Assert.Equal(1, summaryA.Distribution[5]);
            Assert.Equal(["Khách V1", "Khách V2"], summaryA.Reviews.Items.Select(x => x.ReviewerName));

            var summaryB = await svc.GetTourReviewsAsync(tourB, 1, 10);
            Assert.Equal(1, summaryB.ReviewCount);
            Assert.Equal(2.0m, summaryB.AverageRating);

            await Assert.ThrowsAsync<NotFoundException>(() => svc.GetTourReviewsAsync(tourDraft, 1, 10));
        }
    }

    private static Order NewPaidTripOrder(Guid customerId, Guid tripId, string code, Guid policyId, DateTimeOffset now)
        => new()
        {
            OrderId = Guid.NewGuid(),
            OrderCode = code,
            CustomerUserId = customerId,
            OrderType = OrderTypes.TripBooking,
            TotalAmount = 1000000,
            SubtotalAmount = 1000000,
            Status = OrderStatuses.Confirmed,
            PaymentStatus = OrderPaymentStatuses.Paid,
            CreatedAt = now,
            UpdatedAt = now,
            RefundPolicyId = policyId,
            TripBookingOrder = new TripBookingOrder { TripId = tripId, ParticipantCount = 1, GroupingPreference = "Together" }
        };

    private static ParticipantReviewContextRow EligibleContext(
        Guid? participantId = null,
        string tripStatus = TripStatuses.Completed,
        string paymentStatus = OrderPaymentStatuses.Paid,
        string orderStatus = OrderStatuses.Confirmed,
        string participantStatus = BookingParticipantStatuses.Completed,
        bool hasReview = false)
        => new()
        {
            ParticipantId = participantId ?? Guid.NewGuid(),
            TripStatus = tripStatus,
            OrderPaymentStatus = paymentStatus,
            OrderStatus = orderStatus,
            ParticipantStatus = participantStatus,
            HasReview = hasReview
        };

    private static ReviewService CreateService(FakeReviewRepository repo)
        => new(new FakeReviewUnitOfWork(repo, new FakeSystemConfigRepository()), new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid RequiredUserId => userId;
        public string? Jti => null;
        public string? Role => RoleNames.Customer;
        public bool IsAuthenticated => true;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeReviewRepository : IReviewRepository
    {
        public ParticipantReviewContextRow? ParticipantContext { get; set; }
        public Review? ExistingReview { get; set; }
        public Guid? PublicTourFamilyId { get; set; }
        public TourReviewsSummaryRow TourReviewsSummary { get; set; } = new();
        public Review? AddedReview { get; private set; }

        public Task<ParticipantReviewContextRow?> GetParticipantReviewContextAsync(Guid participantId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(ParticipantContext);

        public Task<Review?> GetByParticipantIdForUpdateAsync(Guid participantId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingReview);

        public Task<Guid?> GetPublicTourFamilyIdAsync(Guid tourId, CancellationToken cancellationToken = default)
            => Task.FromResult(PublicTourFamilyId);

        public Task<TourReviewsSummaryRow> GetTourFamilyReviewsAsync(Guid tourFamilyId, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(TourReviewsSummary);

        public Task AddAsync(Review entity, CancellationToken cancellationToken = default)
        {
            AddedReview = entity;
            return Task.CompletedTask;
        }

        public void Update(Review entity) { }

        public Task<(IReadOnlyList<AdminReviewRow> Items, int TotalCount)> GetAdminPagedAsync(AdminReviewFilter filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Review?> GetForUpdateAsync(Guid reviewId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Review?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<Review>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Review> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(Review entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Review> entities) => throw new NotSupportedException();
    }

    private sealed class FakeSystemConfigRepository(Dictionary<string, string>? values = null) : ISystemConfigurationRepository
    {
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(values is not null && values.TryGetValue(key, out var val) ? val : null);

        public Task<List<SystemConfiguration>> GetActiveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemConfiguration?> GetForUpdateAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemConfiguration?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<SystemConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(SystemConfiguration entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<SystemConfiguration> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(SystemConfiguration entity) => throw new NotSupportedException();
        public void Remove(SystemConfiguration entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<SystemConfiguration> entities) => throw new NotSupportedException();
    }

    private sealed class FakeReviewUnitOfWork(IReviewRepository reviews, ISystemConfigurationRepository configs) : IUnitOfWork
    {
        public bool TrySaveResult { get; init; } = true;
        public int SaveCalls { get; private set; }
        public IReviewRepository Reviews => reviews;
        public ISystemConfigurationRepository SystemConfigurations => configs;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public Task<bool> TrySaveChangesIgnoringUniqueViolationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(TrySaveResult);

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }

        public IAdminOperationRepository AdminOperations => throw new NotSupportedException();
        public IAccessTokenRepository AccessTokens => throw new NotSupportedException();
        public IAccountRepository Accounts => throw new NotSupportedException();
        public IActivityAttendanceRepository ActivityAttendances => throw new NotSupportedException();
        public IAppliedPromotionRepository AppliedPromotions => throw new NotSupportedException();
        public IAssetAssignmentRepository AssetAssignments => throw new NotSupportedException();
        public IAuditLogRepository AuditLogs => throw new NotSupportedException();
        public IBookingParticipantRepository BookingParticipants => throw new NotSupportedException();
        public ICategoryRepository Categories => throw new NotSupportedException();
        public IEquipmentAssetRepository EquipmentAssets => throw new NotSupportedException();
        public ILocationRepository Locations => throw new NotSupportedException();
        public INotificationRepository Notifications => throw new NotSupportedException();
        public IOrderRepository Orders => throw new NotSupportedException();
        public IParticipantPostReadRepository ParticipantPostReads => throw new NotSupportedException();
        public IPasswordResetTokenRepository PasswordResetTokens => throw new NotSupportedException();
        public IPaymentRepository Payments => throw new NotSupportedException();
        public IPostRepository Posts => throw new NotSupportedException();
        public IPostSoftSkillRepository PostSoftSkills => throw new NotSupportedException();
        public IPostTagRepository PostTags => throw new NotSupportedException();
        public IProductRepository Products => throw new NotSupportedException();
        public IProductBundleRepository ProductBundles => throw new NotSupportedException();
        public IProductBundleItemRepository ProductBundleItems => throw new NotSupportedException();
        public IProductVariantRepository ProductVariants => throw new NotSupportedException();
        public IProductVariantImageRepository ProductVariantImages => throw new NotSupportedException();
        public IProfileRepository Profiles => throw new NotSupportedException();
        public IPromotionRepository Promotions => throw new NotSupportedException();
        public IPromotionTierRepository PromotionTiers => throw new NotSupportedException();
        public IRefreshTokenRepository RefreshTokens => throw new NotSupportedException();
        public IRefundCaseRepository RefundCases => throw new NotSupportedException();
        public IRefundPolicyRepository RefundPolicies => throw new NotSupportedException();
        public IRefundPolicyRuleRepository RefundPolicyRules => throw new NotSupportedException();
        public IRentalBundleItemSnapshotRepository RentalBundleItemSnapshots => throw new NotSupportedException();
        public IRentalChargeRepository RentalCharges => throw new NotSupportedException();
        public IRentalOrderRepository RentalOrders => throw new NotSupportedException();
        public IRentalOrderItemRepository RentalOrderItems => throw new NotSupportedException();
        public IRentalPriceTierRepository RentalPriceTiers => throw new NotSupportedException();
        public IRoleRepository Roles => throw new NotSupportedException();
        public ISoftSkillRepository SoftSkills => throw new NotSupportedException();
        public ITagRepository Tags => throw new NotSupportedException();
        public ITourRepository Tours => throw new NotSupportedException();
        public ITourActivityRepository TourActivities => throw new NotSupportedException();
        public ITourImageRepository TourImages => throw new NotSupportedException();
        public ITourPostRepository TourPosts => throw new NotSupportedException();
        public ITourPreparationItemRepository TourPreparationItems => throw new NotSupportedException();
        public ITourProvidedItemRepository TourProvidedItems => throw new NotSupportedException();
        public ITourRecurrenceRuleRepository TourRecurrenceRules => throw new NotSupportedException();
        public ITripRepository Trips => throw new NotSupportedException();
        public ITripBookingOrderRepository TripBookingOrders => throw new NotSupportedException();
        public ITripBookingRepository TripBookings => throw new NotSupportedException();
        public ITripGroupRepository TripGroups => throw new NotSupportedException();
        public ITripProvidedItemRepository TripProvidedItems => throw new NotSupportedException();
        public IUserExternalLoginRepository UserExternalLogins => throw new NotSupportedException();
        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class => throw new NotSupportedException();
    }
}
