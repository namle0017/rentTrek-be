using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class TripBookingServiceTests
{
    [Fact]
    public async Task CreateAsync_IncludesBuyerAndUsesServerTripPrice()
    {
        var buyerId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var repository = new FakeTripBookingRepository
        {
            Account = new Account
            {
                UserId = buyerId,
                Email = "Buyer@Example.com",
                FullName = "Người mua",
                Status = "Active"
            },
            Trip = new Trip
            {
                TripId = tripId,
                RefundPolicyId = Guid.NewGuid(),
                PricePerPerson = 1_200_000m,
                TotalSlots = 5,
                Status = "Open",
                StartAt = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)
            }
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        var service = new TripBookingService(
            unitOfWork,
            new FakeCurrentUserService(buyerId),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)),
            new FakeInviteTokenHelper());

        var result = await service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = tripId,
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest
                    {
                        FullName = "Bạn đồng hành",
                        Email = " friend@example.com "
                    }
                ]
            },
            "booking-request-1");

        Assert.Equal(2, result.ParticipantCount);
        Assert.Equal(2_400_000m, result.SubtotalAmount);
        Assert.Equal(2_400_000m, result.TotalAmount);
        Assert.Equal("buyer@example.com", result.Participants[0].Email);
        Assert.Equal(buyerId, result.Participants[0].LinkedUserId);
        Assert.Equal("friend@example.com", result.Participants[1].Email);
        Assert.Null(result.Participants[1].LinkedUserId);
        Assert.NotNull(repository.AddedOrder);
        Assert.Equal("PendingPayment", repository.AddedOrder.Status);
        Assert.Equal("Unpaid", repository.AddedOrder.PaymentStatus);
        Assert.True(unitOfWork.Committed);
        Assert.True(repository.IdempotencyLockAcquired);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateNormalizedEmails()
    {
        var fixture = CreateFixture(totalSlots: 5);
        fixture.Repository.Account!.Email = "same@example.com";

        var exception = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest
                    {
                        FullName = "Người trùng",
                        Email = " SAME@example.com "
                    }
                ]
            },
            "duplicate-email"));

        Assert.Equal("Email participant không được trùng trong cùng booking", exception.Message);
        Assert.Null(fixture.Repository.AddedOrder);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenCapacityIsInsufficient()
    {
        var fixture = CreateFixture(totalSlots: 2, reservedParticipantCount: 1);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest
                    {
                        FullName = "Người thứ hai",
                        Email = "second@example.com"
                    }
                ]
            },
            "capacity-check"));

        Assert.Equal("Chuyến đi không còn đủ chỗ", exception.Message);
        Assert.Null(fixture.Repository.AddedOrder);
    }

    [Fact]
    public async Task CreateAsync_ReplaysExistingBookingForSameIdempotencyKey()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var existing = CreateExistingOrder(fixture.Repository.Account!, fixture.Repository.Trip!);
        fixture.Repository.ExistingOrder = existing;

        var result = await fixture.Service.CreateAsync(
            new CreateTripBookingRequest { TripId = existing.TripBookingOrder.TripId },
            "same-key");

        Assert.Equal(existing.OrderId, result.OrderId);
        Assert.Null(fixture.Repository.AddedOrder);
        Assert.False(fixture.UnitOfWork.TransactionStarted);
    }

    [Fact]
    public async Task CreateAsync_RejectsReusedIdempotencyKeyForDifferentPayload()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var existing = CreateExistingOrder(fixture.Repository.Account!, fixture.Repository.Trip!);
        fixture.Repository.ExistingOrder = existing;

        var exception = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = existing.TripBookingOrder.TripId,
                Note = "different request"
            },
            "same-key"));

        Assert.Equal("Idempotency-Key đã được dùng cho một yêu cầu khác", exception.Message);
        Assert.Null(fixture.Repository.AddedOrder);
    }

    [Fact]
    public async Task GenerateParticipantInvitationsAsync_RejectsUnpaidBooking()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreateExistingOrder(fixture.Repository.Account!, fixture.Repository.Trip!);
        order.Status = "Confirmed";
        order.PaymentStatus = "Unpaid";
        fixture.Repository.OwnedOrder = order;

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Service.GenerateParticipantInvitationsAsync(order.OrderId));

        Assert.Equal("Chỉ tạo lời mời sau khi thanh toán thành công", exception.Message);
        Assert.Empty(fixture.Repository.AddedInvitations);
    }

    [Fact]
    public async Task GenerateParticipantInvitationsAsync_RejectsNonOwner()
    {
        var fixture = CreateFixture(totalSlots: 5);

        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.GenerateParticipantInvitationsAsync(Guid.NewGuid()));

        Assert.Empty(fixture.Repository.AddedInvitations);
    }

    [Fact]
    public async Task GenerateParticipantInvitationsAsync_ReturnsPlainTokenButStoresOnlyHash()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        fixture.Repository.OwnedOrder = order;
        fixture.Repository.SucceededPaymentTotal = order.TotalAmount;

        var invitations = await fixture.Service.GenerateParticipantInvitationsAsync(order.OrderId);

        var invitation = Assert.Single(invitations);
        Assert.Equal("invite-token", invitation.Token);
        var stored = Assert.Single(fixture.Repository.AddedInvitations);
        Assert.Equal("opaque-token-hash", stored.TokenHash);
        Assert.DoesNotContain("invite-token", stored.TokenHash);
        Assert.True(fixture.UnitOfWork.Committed);
        Assert.True(fixture.Repository.BookingLockAcquired);
    }

    [Fact]
    public async Task ClaimParticipantInvitationAsync_LinksMatchingAccountAndConsumesToken()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        var guest = order.TripBookingOrder.Participants.Single(x => x.LinkedUserId is null);
        var guestUserId = Guid.NewGuid();
        fixture.Repository.Account = new Account
        {
            UserId = guestUserId,
            Email = guest.Email.ToUpperInvariant(),
            FullName = guest.FullName,
            Status = "Active"
        };
        fixture.CurrentUser.RequiredUserId = guestUserId;
        fixture.Repository.Invitation = new ParticipantInvitation
        {
            ParticipantInvitationId = Guid.NewGuid(),
            ParticipantId = guest.ParticipantId,
            BookingOrderId = order.OrderId,
            CreatedByUserId = order.CustomerUserId,
            TokenHash = "opaque-token-hash",
            ExpiresAt = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
            Participant = guest,
            BookingOrder = order.TripBookingOrder
        };

        var result = await fixture.Service.ClaimParticipantInvitationAsync("invite-token");

        Assert.Equal(guestUserId, guest.LinkedUserId);
        Assert.Equal(guestUserId, fixture.Repository.Invitation.RedeemedByUserId);
        Assert.NotNull(fixture.Repository.Invitation.RedeemedAt);
        Assert.False(result.CanManage);
        Assert.Single(result.Participants);
        Assert.True(fixture.Repository.BookingLockAcquired);
    }

    [Fact]
    public async Task ClaimParticipantInvitationAsync_RejectsAccountWithDifferentEmail()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        var guest = order.TripBookingOrder.Participants.Single(x => x.LinkedUserId is null);
        var guestUserId = Guid.NewGuid();
        fixture.Repository.Account = new Account
        {
            UserId = guestUserId,
            Email = "wrong@example.com",
            FullName = "Sai người",
            Status = "Active"
        };
        fixture.CurrentUser.RequiredUserId = guestUserId;
        fixture.Repository.Invitation = new ParticipantInvitation
        {
            ParticipantInvitationId = Guid.NewGuid(),
            ParticipantId = guest.ParticipantId,
            BookingOrderId = order.OrderId,
            CreatedByUserId = order.CustomerUserId,
            TokenHash = "opaque-token-hash",
            ExpiresAt = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            Participant = guest,
            BookingOrder = order.TripBookingOrder
        };

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.Service.ClaimParticipantInvitationAsync("invite-token"));

        Assert.Equal("Email tài khoản không khớp participant được mời", exception.Message);
        Assert.Null(guest.LinkedUserId);
        Assert.Null(fixture.Repository.Invitation.RedeemedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClaimParticipantInvitationAsync_RejectsExpiredOrRedeemedToken(bool expired)
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        var guest = order.TripBookingOrder.Participants.Single(x => x.LinkedUserId is null);
        fixture.Repository.Invitation = new ParticipantInvitation
        {
            ParticipantInvitationId = Guid.NewGuid(),
            ParticipantId = guest.ParticipantId,
            BookingOrderId = order.OrderId,
            CreatedByUserId = order.CustomerUserId,
            TokenHash = "opaque-token-hash",
            ExpiresAt = expired
                ? new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero)
                : new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            RedeemedAt = expired
                ? null
                : new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero),
            Participant = guest,
            BookingOrder = order.TripBookingOrder
        };

        await Assert.ThrowsAsync<ConflictException>(
            () => fixture.Service.ClaimParticipantInvitationAsync("invite-token"));

        Assert.Null(guest.LinkedUserId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsFullBookingToOwner()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        fixture.Repository.AccessOrder = order;

        var result = await fixture.Service.GetByIdAsync(order.OrderId);

        Assert.True(result.CanManage);
        Assert.Equal(2, result.ParticipantCount);
        Assert.Equal(2, result.Participants.Count);
        Assert.Equal(order.TotalAmount, result.TotalAmount);
        Assert.Equal("Paid", result.PaymentStatus);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOnlyOwnParticipantAndHidesMoneyFromLinkedGuest()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        var guest = order.TripBookingOrder.Participants.Single(x => x.LinkedUserId is null);
        var guestUserId = Guid.NewGuid();
        guest.LinkedUserId = guestUserId;
        fixture.CurrentUser.RequiredUserId = guestUserId;
        fixture.Repository.AccessOrder = order;

        var result = await fixture.Service.GetByIdAsync(order.OrderId);

        Assert.False(result.CanManage);
        Assert.Equal(1, result.ParticipantCount);
        Assert.Equal(guest.ParticipantId, Assert.Single(result.Participants).ParticipantId);
        Assert.Equal(0m, result.UnitPrice);
        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(string.Empty, result.PaymentStatus);
        Assert.Null(result.HoldExpiresAt);
        Assert.Equal(order.TripBookingOrder.Trip.StartAt, result.TripStartAt);
        Assert.Equal(order.TripBookingOrder.Trip.EndAt, result.TripEndAt);
        Assert.Equal(order.TripBookingOrder.Trip.Status, result.TripStatus);
    }

    private static TestFixture CreateFixture(int totalSlots, int reservedParticipantCount = 0)
    {
        var buyerId = Guid.NewGuid();
        var repository = new FakeTripBookingRepository
        {
            Account = new Account
            {
                UserId = buyerId,
                Email = "buyer@example.com",
                FullName = "Người mua",
                Status = "Active"
            },
            Trip = new Trip
            {
                TripId = Guid.NewGuid(),
                RefundPolicyId = Guid.NewGuid(),
                PricePerPerson = 1_000_000m,
                TotalSlots = totalSlots,
                Status = "Open",
                StartAt = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)
            },
            ReservedParticipantCount = reservedParticipantCount
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        var currentUser = new FakeCurrentUserService(buyerId);
        var service = new TripBookingService(
            unitOfWork,
            currentUser,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)),
            new FakeInviteTokenHelper());
        return new TestFixture(service, repository, unitOfWork, currentUser);
    }

    private static Order CreateExistingOrder(Account account, Trip trip)
    {
        var orderId = Guid.NewGuid();
        var booking = new TripBookingOrder
        {
            OrderId = orderId,
            TripId = trip.TripId,
            Trip = trip,
            ParticipantCount = 1,
            UnitPrice = trip.PricePerPerson,
            GroupingPreference = "KeepTogether",
            Participants =
            [
                new BookingParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    BookingOrderId = orderId,
                    LinkedUserId = account.UserId,
                    FullName = account.FullName,
                    Email = account.Email,
                    Status = "Pending",
                    TripId = trip.TripId
                }
            ]
        };
        var order = new Order
        {
            OrderId = orderId,
            CustomerUserId = account.UserId,
            OrderCode = "RT-EXISTING",
            Status = "PendingPayment",
            PaymentStatus = "Unpaid",
            SubtotalAmount = trip.PricePerPerson,
            TotalAmount = trip.PricePerPerson,
            TripBookingOrder = booking
        };
        booking.Order = order;
        return order;
    }

    private static Order CreatePaidOrderWithGuest(Account account, Trip trip)
    {
        var order = CreateExistingOrder(account, trip);
        var guest = new BookingParticipant
        {
            ParticipantId = Guid.NewGuid(),
            BookingOrderId = order.OrderId,
            FullName = "Khách được mời",
            Email = "guest@example.com",
            Status = "Pending",
            TripId = trip.TripId
        };
        order.TripBookingOrder.Participants.Add(guest);
        order.TripBookingOrder.ParticipantCount = 2;
        order.Status = "Confirmed";
        order.PaymentStatus = "Paid";
        order.TotalAmount = trip.PricePerPerson * 2;
        order.SubtotalAmount = order.TotalAmount;
        return order;
    }

    private sealed record TestFixture(
        TripBookingService Service,
        FakeTripBookingRepository Repository,
        FakeUnitOfWork UnitOfWork,
        FakeCurrentUserService CurrentUser);

    private sealed class FakeCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid RequiredUserId { get; set; } = userId;
        public Guid? UserId => RequiredUserId;
        public string? Jti => null;
        public string? Role => null;
        public bool IsAuthenticated => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeInviteTokenHelper : IInviteTokenHelper
    {
        public string CreateToken() => "invite-token";
        public string HashToken(string token) => "opaque-token-hash";
    }

    private sealed class FakeUnitOfWork(ITripBookingRepository repository) : IUnitOfWork
    {
        public ITripBookingRepository TripBookings { get; } = repository;
        public IAccountRepository Accounts => throw new NotSupportedException();
        public IRoleRepository Roles => throw new NotSupportedException();
        public IRefreshTokenRepository RefreshTokens => throw new NotSupportedException();
        public IAccessTokenRepository AccessTokens => throw new NotSupportedException();
        public ITourRepository Tours => throw new NotSupportedException();
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
        public IRefundPolicyRepository RefundPolicies => throw new NotSupportedException();
        public IRefundPolicyRuleRepository RefundPolicyRules => throw new NotSupportedException();
        public IRentalBundleItemSnapshotRepository RentalBundleItemSnapshots => throw new NotSupportedException();
        public IRentalChargeRepository RentalCharges => throw new NotSupportedException();
        public IRentalOrderRepository RentalOrders => throw new NotSupportedException();
        public IRentalOrderItemRepository RentalOrderItems => throw new NotSupportedException();
        public IRentalPriceTierRepository RentalPriceTiers => throw new NotSupportedException();
        public IReviewRepository Reviews => throw new NotSupportedException();
        public ISoftSkillRepository SoftSkills => throw new NotSupportedException();
        public ISystemConfigurationRepository SystemConfigurations => throw new NotSupportedException();
        public ITagRepository Tags => throw new NotSupportedException();
        public ITourActivityRepository TourActivities => throw new NotSupportedException();
        public ITourImageRepository TourImages => throw new NotSupportedException();
        public ITourPostRepository TourPosts => throw new NotSupportedException();
        public ITourPreparationItemRepository TourPreparationItems => throw new NotSupportedException();
        public ITourProvidedItemRepository TourProvidedItems => throw new NotSupportedException();
        public ITourRecurrenceRuleRepository TourRecurrenceRules => throw new NotSupportedException();
        public ITripRepository Trips => throw new NotSupportedException();
        public ITripBookingOrderRepository TripBookingOrders => throw new NotSupportedException();
        public ITripGroupRepository TripGroups => throw new NotSupportedException();
        public ITripProvidedItemRepository TripProvidedItems => throw new NotSupportedException();
        public IUserExternalLoginRepository UserExternalLogins => throw new NotSupportedException();
        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class => throw new NotSupportedException();
        public bool Committed { get; private set; }
        public bool TransactionStarted { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            TransactionStarted = true;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeTripBookingRepository : ITripBookingRepository
    {
        public Account? Account { get; set; }
        public Trip? Trip { get; init; }
        public int ReservedParticipantCount { get; init; }
        public Order? ExistingOrder { get; set; }
        public Order? AccessOrder { get; set; }
        public Order? OwnedOrder { get; set; }
        public ParticipantInvitation? Invitation { get; set; }
        public decimal SucceededPaymentTotal { get; set; }
        public Order? AddedOrder { get; private set; }
        public List<ParticipantInvitation> AddedInvitations { get; } = [];
        public bool IdempotencyLockAcquired { get; private set; }
        public bool BookingLockAcquired { get; private set; }

        public Task AcquireIdempotencyLockAsync(
            Guid customerUserId,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            IdempotencyLockAcquired = true;
            return Task.CompletedTask;
        }

        public Task AcquireBookingLockAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            BookingLockAcquired = true;
            return Task.CompletedTask;
        }

        public Task AddInvitationAsync(ParticipantInvitation invitation, CancellationToken cancellationToken = default)
        {
            AddedInvitations.Add(invitation);
            return Task.CompletedTask;
        }

        public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            AddedOrder = order;
            return Task.CompletedTask;
        }

        public Task<Account?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Account);

        public Task<IReadOnlyList<ParticipantInvitation>> GetOutstandingInvitationsAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParticipantInvitation>>([]);

        public Task<Guid?> GetInvitationBookingOrderIdAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(Invitation?.BookingOrderId);

        public Task<Order?> GetByIdempotencyKeyAsync(
            Guid customerUserId,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingOrder);

        public Task<Order?> GetForAccessAsync(
            Guid orderId,
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(AccessOrder);

        public Task<ParticipantInvitation?> GetInvitationByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Invitation);

        public Task<Order?> GetOwnedBookingAsync(
            Guid orderId,
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(OwnedOrder);

        public Task<int> GetReservedParticipantCountAsync(
            Guid tripId,
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ReservedParticipantCount);

        public Task<decimal> GetSucceededPaymentTotalAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(SucceededPaymentTotal);

        public Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(Trip);
    }
}
