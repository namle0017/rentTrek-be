using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Kiểm thử nghiệp vụ danh sách chuyến tham gia và theo dõi đọc bài viết của người tham gia (T1).
/// </summary>
public sealed class ParticipationServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetMyParticipations_MapsRowsAndComputesReviewEligibility()
    {
        var userId = Guid.NewGuid();
        var trip1Id = Guid.NewGuid();
        var trip2Id = Guid.NewGuid();
        var tourId = Guid.NewGuid();
        var reviewedOrderId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var repo = new FakeParticipantPostReadRepository
        {
            MyParticipationsResult =
            [
                new MyParticipationRow
                {
                    ParticipantId = Guid.NewGuid(),
                    TripId = trip1Id,
                    TourId = tourId,
                    TourName = "Tà Năng - Phan Dũng",
                    StartAt = FixedNow.AddDays(-5),
                    EndAt = FixedNow.AddDays(-3),
                    TripStatus = TripStatuses.Completed,
                    ParticipantStatus = BookingParticipantStatuses.Completed,
                    OrderStatus = OrderStatuses.Confirmed,
                    OrderPaymentStatus = OrderPaymentStatuses.Paid,
                    UnreadPostCount = 2,
                    HasReview = false
                },
                new MyParticipationRow
                {
                    ParticipantId = Guid.NewGuid(),
                    TripId = trip2Id,
                    TourId = tourId,
                    TourName = "Bidoup",
                    StartAt = FixedNow.AddDays(5),
                    EndAt = FixedNow.AddDays(6),
                    TripStatus = TripStatuses.Open,
                    ParticipantStatus = BookingParticipantStatuses.Pending,
                    OrderStatus = OrderStatuses.Confirmed,
                    OrderPaymentStatus = OrderPaymentStatuses.Unpaid,
                    UnreadPostCount = 0,
                    HasReview = false
                },
                new MyParticipationRow
                {
                    ParticipantId = Guid.NewGuid(),
                    TripId = trip1Id,
                    TourId = tourId,
                    TourName = "Tà Năng - Đã hoàn 1 phần",
                    StartAt = FixedNow.AddDays(-5),
                    EndAt = FixedNow.AddDays(-3),
                    TripStatus = TripStatuses.Completed,
                    ParticipantStatus = BookingParticipantStatuses.Completed,
                    OrderStatus = OrderStatuses.Confirmed,
                    OrderPaymentStatus = OrderPaymentStatuses.PartiallyRefunded,
                    UnreadPostCount = 0,
                    HasReview = false
                },
                new MyParticipationRow
                {
                    ParticipantId = Guid.NewGuid(),
                    TripId = trip1Id,
                    TourId = tourId,
                    TourName = "Tà Năng - Đã đánh giá",
                    StartAt = FixedNow.AddDays(-5),
                    EndAt = FixedNow.AddDays(-3),
                    TripStatus = TripStatuses.Completed,
                    ParticipantStatus = BookingParticipantStatuses.Completed,
                    OrderStatus = OrderStatuses.Confirmed,
                    OrderPaymentStatus = OrderPaymentStatuses.Paid,
                    UnreadPostCount = 0,
                    HasReview = true,
                    OrderId = reviewedOrderId,
                    ReviewId = reviewId,
                    ReviewRating = 4,
                    ReviewComment = "Chuyến rất tuyệt"
                },
                new MyParticipationRow
                {
                    ParticipantId = Guid.NewGuid(),
                    TripId = trip1Id,
                    TourId = tourId,
                    TourName = "Tà Năng - Đơn hủy",
                    StartAt = FixedNow.AddDays(-5),
                    EndAt = FixedNow.AddDays(-3),
                    TripStatus = TripStatuses.Completed,
                    ParticipantStatus = BookingParticipantStatuses.Completed,
                    OrderStatus = OrderStatuses.Cancelled,
                    OrderPaymentStatus = OrderPaymentStatuses.Paid,
                    UnreadPostCount = 0,
                    HasReview = false
                }
            ]
        };

        var service = CreateService(repo, userId, FixedNow);
        var result = await service.GetMyParticipationsAsync();

        Assert.Equal(5, result.Count);
        Assert.Equal("Tà Năng - Phan Dũng", result[0].TourName);
        Assert.Equal(2, result[0].UnreadPostCount);
        Assert.True(result[0].CanReview);
        Assert.False(result[0].HasReview);
        Assert.False(result[1].CanReview);
        Assert.True(result[2].CanReview);
        Assert.False(result[3].CanReview);
        Assert.True(result[3].HasReview);
        Assert.Equal(reviewedOrderId, result[3].OrderId);
        Assert.Equal(reviewId, result[3].ReviewId);
        Assert.Equal(4, result[3].ReviewRating);
        Assert.Equal("Chuyến rất tuyệt", result[3].ReviewComment);
        Assert.Null(result[0].ReviewRating);
        Assert.False(result[4].CanReview);
        Assert.Equal(userId, repo.RequestedUserId);
    }

    [Fact]
    public async Task GetParticipantPosts_WhenNotOwnedOrCancelled_Throws404()
    {
        var repo = new FakeParticipantPostReadRepository { OwnedActiveResult = false };
        var service = CreateService(repo, Guid.NewGuid(), FixedNow);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetParticipantPostsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetParticipantPosts_WhenOwned_ReturnsOrderedPostsWithReadState()
    {
        var participantId = Guid.NewGuid();
        var readAt = FixedNow.UtcDateTime.AddHours(-2);
        var repo = new FakeParticipantPostReadRepository
        {
            OwnedActiveResult = true,
            ParticipantPostsResult =
            [
                new ParticipantPostRow { PostId = Guid.NewGuid(), Title = "Checklist 1", Slug = "checklist-1", ReadAt = readAt },
                new ParticipantPostRow { PostId = Guid.NewGuid(), Title = "Checklist 2", Slug = "checklist-2", ReadAt = null }
            ]
        };

        var service = CreateService(repo, Guid.NewGuid(), FixedNow);
        var result = await service.GetParticipantPostsAsync(participantId);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].IsRead);
        Assert.Equal(readAt, result[0].ReadAt);
        Assert.False(result[1].IsRead);
        Assert.Null(result[1].ReadAt);
    }

    [Fact]
    public async Task MarkPostAsRead_WhenPostNotAttachedOrNotOwned_Throws404()
    {
        var repo = new FakeParticipantPostReadRepository { CanAccessPostResult = false };
        var uow = new FakeParticipationUnitOfWork(repo);
        var service = new ParticipationService(uow, new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkPostAsReadAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Null(repo.AddedRead);
        Assert.Equal(0, uow.TrySaveCalls);
    }

    [Fact]
    public async Task MarkPostAsRead_FirstRead_AddsRecordWithUtcNow()
    {
        var participantId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var repo = new FakeParticipantPostReadRepository { CanAccessPostResult = true };
        var uow = new FakeParticipationUnitOfWork(repo);
        var service = new ParticipationService(uow, new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

        var result = await service.MarkPostAsReadAsync(participantId, postId);

        Assert.True(result.IsRead);
        Assert.Equal(FixedNow.UtcDateTime, result.ReadAt);
        Assert.NotNull(repo.AddedRead);
        Assert.Equal(1, uow.TrySaveCalls);
    }

    [Fact]
    public async Task MarkPostAsRead_AlreadyRead_ReturnsOriginalReadAtWithoutWriting()
    {
        var participantId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var originalTime = FixedNow.UtcDateTime.AddDays(-1);
        var existing = new ParticipantPostRead
        {
            ParticipantPostReadId = Guid.NewGuid(),
            ParticipantId = participantId,
            PostId = postId,
            ReadAt = originalTime,
            CreatedAt = originalTime
        };
        var repo = new FakeParticipantPostReadRepository { CanAccessPostResult = true, ExistingRead = existing };
        var uow = new FakeParticipationUnitOfWork(repo);
        var service = new ParticipationService(uow, new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

        var result = await service.MarkPostAsReadAsync(participantId, postId);

        Assert.Equal(existing.ParticipantPostReadId, result.ParticipantPostReadId);
        Assert.Equal(originalTime, result.ReadAt);
        Assert.Null(repo.AddedRead);
        Assert.Equal(0, uow.TrySaveCalls);
    }

    [Fact]
    public async Task MarkPostAsRead_ConcurrentUniqueViolation_ReloadsWinnerRecordWithoutError()
    {
        var participantId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var winnerTime = FixedNow.UtcDateTime.AddSeconds(-2);
        var winner = new ParticipantPostRead
        {
            ParticipantPostReadId = Guid.NewGuid(),
            ParticipantId = participantId,
            PostId = postId,
            ReadAt = winnerTime,
            CreatedAt = winnerTime
        };

        var repo = new FakeParticipantPostReadRepository { CanAccessPostResult = true };
        var uow = new FakeParticipationUnitOfWork(repo)
        {
            OnTrySave = () =>
            {
                // Mô phỏng request song song vừa kịp chèn bản ghi trước khi SaveChanges chạy
                repo.ExistingRead = winner;
                return false;
            }
        };
        var service = new ParticipationService(uow, new FixedCurrentUser(Guid.NewGuid()), new FixedClock(FixedNow));

        var result = await service.MarkPostAsReadAsync(participantId, postId);

        Assert.Equal(winner.ParticipantPostReadId, result.ParticipantPostReadId);
        Assert.Equal(winnerTime, result.ReadAt);
        Assert.Equal(1, uow.TrySaveCalls);
    }

    [AdminPostgresFact]
    public async Task ParticipationPosts_Postgres_IdempotentConcurrentStatsUnreadAndAccessRules()
    {
        await using var db = new AdminTestDatabase();
        await db.InitializeAsync();
        var t0 = db.Now;
        var post1Id = Guid.NewGuid();
        var post2Id = Guid.NewGuid();
        var draftPostId = Guid.NewGuid();
        var otherTourPostId = Guid.NewGuid();
        var cancelledParticipantId = Guid.NewGuid();
        var tour2Id = Guid.NewGuid();

        await using (var setup = db.Context())
        {
            var participantA = await setup.BookingParticipants.SingleAsync(x => x.ParticipantId == db.ParticipantId);
            participantA.LinkedUserId = db.CustomerId;
            participantA.CreatedAt = t0.AddDays(-1);

            var tourOrder = await setup.Orders.SingleAsync(x => x.OrderCode == "TOUR-TEST");
            setup.BookingParticipants.Add(new BookingParticipant
            {
                ParticipantId = cancelledParticipantId,
                BookingOrderId = tourOrder.OrderId,
                LinkedUserId = db.CustomerId,
                TripId = db.TripId,
                FullName = "Cancelled Slot",
                Status = BookingParticipantStatuses.Cancelled,
                CreatedAt = t0,
                UpdatedAt = t0
            });

            var trip = await setup.Trips.SingleAsync(x => x.TripId == db.TripId);
            var existingTour = await setup.Tours.SingleAsync(x => x.Id == trip.TourId);
            setup.Tours.Add(new Tour
            {
                Id = tour2Id,
                Name = "Tour Khác",
                CategoryId = existingTour.CategoryId,
                MainLocationId = existingTour.MainLocationId,
                CreatedByUserId = db.AdminId,
                Status = TourStatuses.Published
            });

            setup.Posts.AddRange(
                NewPost(post2Id, db.AdminId, "Chuẩn bị giày", "chuan-bi-giay", PostStatuses.Published, t0.UtcDateTime),
                NewPost(post1Id, db.AdminId, "Quy định an toàn", "quy-dinh-an-toan", PostStatuses.Published, t0.UtcDateTime),
                NewPost(draftPostId, db.AdminId, "Bản nháp", "ban-nhap", PostStatuses.Draft, t0.UtcDateTime),
                NewPost(otherTourPostId, db.AdminId, "Bài tour khác", "bai-tour-khac", PostStatuses.Published, t0.UtcDateTime));

            setup.TourPosts.AddRange(
                new TourPost { TourPostId = Guid.NewGuid(), TourId = trip.TourId, PostId = post1Id, DisplayOrder = 1, CreatedAt = t0.UtcDateTime },
                new TourPost { TourPostId = Guid.NewGuid(), TourId = trip.TourId, PostId = post2Id, DisplayOrder = 2, CreatedAt = t0.UtcDateTime },
                new TourPost { TourPostId = Guid.NewGuid(), TourId = trip.TourId, PostId = draftPostId, DisplayOrder = 3, CreatedAt = t0.UtcDateTime },
                new TourPost { TourPostId = Guid.NewGuid(), TourId = tour2Id, PostId = otherTourPostId, DisplayOrder = 1, CreatedAt = t0.UtcDateTime });

            await setup.SaveChangesAsync();
        }

        // User A xem participations & posts ban đầu
        await using (var ctx = db.Context())
        {
            var svc = new ParticipationService(new UnitOfWork(ctx), new FixedCurrentUser(db.CustomerId), new FixedClock(t0));
            var my = await svc.GetMyParticipationsAsync();
            var item = Assert.Single(my);
            Assert.Equal(db.ParticipantId, item.ParticipantId);
            Assert.Equal(2, item.UnreadPostCount);

            var posts = await svc.GetParticipantPostsAsync(db.ParticipantId);
            Assert.Equal([post1Id, post2Id], posts.Select(p => p.PostId));
            Assert.All(posts, p => Assert.False(p.IsRead));
        }

        // User B -> 404; bài tour khác -> 404; bài Draft -> 404; participant Cancelled -> 404
        await using (var ctx = db.Context())
        {
            var userBSvc = new ParticipationService(new UnitOfWork(ctx), new FixedCurrentUser(db.StaffId), new FixedClock(t0));
            await Assert.ThrowsAsync<NotFoundException>(() => userBSvc.GetParticipantPostsAsync(db.ParticipantId));
            await Assert.ThrowsAsync<NotFoundException>(() => userBSvc.MarkPostAsReadAsync(db.ParticipantId, post1Id));

            var userASvc = new ParticipationService(new UnitOfWork(ctx), new FixedCurrentUser(db.CustomerId), new FixedClock(t0));
            await Assert.ThrowsAsync<NotFoundException>(() => userASvc.MarkPostAsReadAsync(db.ParticipantId, otherTourPostId));
            await Assert.ThrowsAsync<NotFoundException>(() => userASvc.MarkPostAsReadAsync(db.ParticipantId, draftPostId));
            await Assert.ThrowsAsync<NotFoundException>(() => userASvc.GetParticipantPostsAsync(cancelledParticipantId));
        }

        // Gửi POST .../read 3 lần liên tiếp ở các thời điểm khác nhau -> đúng 1 dòng DB, ReadAt giữ nguyên t0
        for (var i = 0; i < 3; i++)
        {
            await using var ctx = db.Context();
            var svc = new ParticipationService(new UnitOfWork(ctx), new FixedCurrentUser(db.CustomerId), new FixedClock(t0.AddMinutes(i * 10)));
            var read = await svc.MarkPostAsReadAsync(db.ParticipantId, post1Id);
            Assert.Equal(t0.UtcDateTime, read.ReadAt);
        }

        // Thử ép ghi trùng trực tiếp qua UnitOfWork -> Postgres 23505 được bắt an toàn, trả false và tách entity
        await using (var raceCtx = db.Context())
        {
            var uow = new UnitOfWork(raceCtx);
            await uow.ParticipantPostReads.AddAsync(new ParticipantPostRead
            {
                ParticipantPostReadId = Guid.NewGuid(),
                ParticipantId = db.ParticipantId,
                PostId = post1Id,
                ReadAt = t0.UtcDateTime.AddHours(1),
                CreatedAt = t0.UtcDateTime.AddHours(1)
            });
            Assert.False(await uow.TrySaveChangesIgnoringUniqueViolationAsync());
            Assert.Empty(raceCtx.ChangeTracker.Entries());
        }

        await using (var verify = db.Context())
        {
            var count = await verify.ParticipantPostReads.CountAsync(r => r.ParticipantId == db.ParticipantId && r.PostId == post1Id);
            Assert.Equal(1, count);
            var stored = await verify.ParticipantPostReads.SingleAsync(r => r.ParticipantId == db.ParticipantId && r.PostId == post1Id);
            Assert.Equal(t0.UtcDateTime, stored.ReadAt);

            var uow = new UnitOfWork(verify);
            var (total, read) = await uow.Posts.GetReadStatsAsync(post1Id);
            Assert.Equal(1, total);
            Assert.Equal(1, read);

            var svc = new ParticipationService(uow, new FixedCurrentUser(db.CustomerId), new FixedClock(t0));
            var afterRead = await svc.GetMyParticipationsAsync();
            Assert.Equal(1, Assert.Single(afterRead).UnreadPostCount);

            // Archive post1 -> ẩn khỏi danh sách nhưng bản ghi đọc vẫn giữ nguyên
            var post1 = await verify.Posts.SingleAsync(p => p.PostId == post1Id);
            post1.Status = PostStatuses.Archived;
            await verify.SaveChangesAsync();

            var postsAfterArchive = await svc.GetParticipantPostsAsync(db.ParticipantId);
            Assert.Equal([post2Id], postsAfterArchive.Select(p => p.PostId));
            Assert.Equal(1, await verify.ParticipantPostReads.CountAsync(r => r.ParticipantId == db.ParticipantId && r.PostId == post1Id));
        }
    }

    private static Post NewPost(Guid postId, Guid authorId, string title, string slug, string status, DateTime now)
        => new()
        {
            PostId = postId,
            CreatedByUserId = authorId,
            Title = title,
            Slug = slug,
            PostType = "Guide",
            Content = "Nội dung hướng dẫn",
            Status = status,
            PublishedAt = status == PostStatuses.Published ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static ParticipationService CreateService(FakeParticipantPostReadRepository repo, Guid userId, DateTimeOffset now)
        => new(new FakeParticipationUnitOfWork(repo), new FixedCurrentUser(userId), new FixedClock(now));

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

    private sealed class FakeParticipantPostReadRepository : IParticipantPostReadRepository
    {
        public IReadOnlyList<MyParticipationRow> MyParticipationsResult { get; set; } = [];
        public bool OwnedActiveResult { get; set; }
        public IReadOnlyList<ParticipantPostRow> ParticipantPostsResult { get; set; } = [];
        public bool CanAccessPostResult { get; set; }
        public ParticipantPostRead? ExistingRead { get; set; }
        public ParticipantPostRead? AddedRead { get; private set; }
        public Guid? RequestedUserId { get; private set; }

        public Task<IReadOnlyList<MyParticipationRow>> GetMyParticipationsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            RequestedUserId = userId;
            return Task.FromResult(MyParticipationsResult);
        }

        public Task<bool> IsOwnedActiveParticipantAsync(Guid participantId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(OwnedActiveResult);

        public Task<IReadOnlyList<ParticipantPostRow>> GetParticipantPostsAsync(Guid participantId, CancellationToken cancellationToken = default)
            => Task.FromResult(ParticipantPostsResult);

        public Task<bool> CanAccessTourPostAsync(Guid participantId, Guid userId, Guid postId, CancellationToken cancellationToken = default)
            => Task.FromResult(CanAccessPostResult);

        public Task<ParticipantPostRead?> GetByParticipantAndPostAsync(Guid participantId, Guid postId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingRead);

        public Task AddAsync(ParticipantPostRead entity, CancellationToken cancellationToken = default)
        {
            AddedRead = entity;
            return Task.CompletedTask;
        }

        public Task<ParticipantPostRead?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ParticipantPostRead>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<ParticipantPostRead> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(ParticipantPostRead entity) => throw new NotSupportedException();
        public void Remove(ParticipantPostRead entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<ParticipantPostRead> entities) => throw new NotSupportedException();
    }

    private sealed class FakeParticipationUnitOfWork(IParticipantPostReadRepository postReads) : IUnitOfWork
    {
        public Func<bool>? OnTrySave { get; init; }
        public int TrySaveCalls { get; private set; }
        public IParticipantPostReadRepository ParticipantPostReads => postReads;

        public Task<bool> TrySaveChangesIgnoringUniqueViolationAsync(CancellationToken cancellationToken = default)
        {
            TrySaveCalls++;
            return Task.FromResult(OnTrySave?.Invoke() ?? true);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
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
        public IRentalPolicyRepository RentalPolicies => throw new NotSupportedException();
        public IRentalEvidencePhotoRepository RentalEvidencePhotos => throw new NotSupportedException();
        public IRentalReportRepository RentalReports => throw new NotSupportedException();
        public IRentalSettlementRepository RentalSettlements => throw new NotSupportedException();
        public IReviewRepository Reviews => throw new NotSupportedException();
        public IRoleRepository Roles => throw new NotSupportedException();
        public ISoftSkillRepository SoftSkills => throw new NotSupportedException();
        public ISystemConfigurationRepository SystemConfigurations => throw new NotSupportedException();
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
