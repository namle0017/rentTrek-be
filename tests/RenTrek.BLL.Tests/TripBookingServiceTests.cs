using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class TripBookingServiceTests
{
    private const string DefaultContactPhone = "0901234567";

    [Fact]
    public async Task CreateAsync_UsesBuyerForeignKeyWithoutAddingDetachedAccountToOrderGraph()
    {
        var fixture = CreateFixture(totalSlots: 5);

        await fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                ContactPhone = DefaultContactPhone
            },
            "detached-buyer");

        Assert.Equal(fixture.Repository.Account!.UserId, fixture.Repository.AddedOrder!.CustomerUserId);
        Assert.Null(fixture.Repository.AddedOrder.CustomerUser);
    }

    [Fact]
    public async Task CreateAsync_CompanionWithoutEmailOrPhone_UsesBuyerAsEmergencyContactAndCountsSlots()
    {
        var fixture = CreateFixture(totalSlots: 5);

        var result = await fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                ContactPhone = " 0909000111 ",
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest { FullName = "Bé An", Email = "", Phone = "  " },
                    new CreateBookingParticipantRequest { FullName = "Bác Bình", Email = null, Phone = null }
                ]
            },
            "no-email-no-phone");

        Assert.Equal(3, result.ParticipantCount);
        Assert.Equal(3_000_000m, result.TotalAmount);
        var persisted = fixture.Repository.AddedOrder!.TripBookingOrder.Participants.ToList();
        Assert.Equal("0909000111", persisted[0].Phone);
        Assert.Equal("buyer@example.com", persisted[0].Email);
        Assert.All(persisted.Skip(1), child =>
        {
            Assert.Null(child.Email);
            Assert.Null(child.Phone);
            Assert.Equal("Người mua", child.EmergencyContactName);
            Assert.Equal("0909000111", child.EmergencyContactPhone);
        });
    }

    [Fact]
    public async Task CreateAsync_CompanionWithOwnPhone_StoresPhoneWithoutBuyerEmergencyFallback()
    {
        var fixture = CreateFixture(totalSlots: 5);

        await fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                ContactPhone = DefaultContactPhone,
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest
                    {
                        FullName = "Bạn có số",
                        Email = "friend@example.com",
                        Phone = " 0988777666 "
                    }
                ]
            },
            "own-phone");

        var companion = fixture.Repository.AddedOrder!.TripBookingOrder.Participants.Single(x => x.Email == "friend@example.com");
        Assert.Equal("0988777666", companion.Phone);
        Assert.Null(companion.EmergencyContactName);
        Assert.Null(companion.EmergencyContactPhone);
    }

    [Fact]
    public async Task CreateAsync_BookForOthers_ExcludesBuyerFromSlotsAndPriceAndAllowsBuyerEmailOnCompanion()
    {
        var fixture = CreateFixture(totalSlots: 5);

        var result = await fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                ContactPhone = DefaultContactPhone,
                BuyerParticipates = false,
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest
                    {
                        FullName = "Con trai dùng email bố",
                        Email = "BUYER@example.com"
                    }
                ]
            },
            "book-for-others");

        Assert.Equal(1, result.ParticipantCount);
        Assert.Equal(1_000_000m, result.SubtotalAmount);
        Assert.Equal(1_000_000m, result.TotalAmount);
        Assert.True(result.CanManage);
        var onlySeat = Assert.Single(result.Participants);
        Assert.Equal("Con trai dùng email bố", onlySeat.FullName);
        Assert.Equal("buyer@example.com", onlySeat.Email);
    }

    [Fact]
    public async Task CreateAsync_BookForOthersWithoutCompanions_ReturnsBadRequest()
    {
        var fixture = CreateFixture(totalSlots: 5);

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                ContactPhone = DefaultContactPhone,
                BuyerParticipates = false,
                AdditionalParticipants = []
            },
            "empty-book-for-others"));
    }

    [Fact]
    public async Task GetByIdAsync_BookForOthersBuyerCanStillViewAndManageOrder()
    {
        var fixture = CreateFixture(totalSlots: 5);
        await fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = fixture.Repository.Trip!.TripId,
                ContactPhone = DefaultContactPhone,
                BuyerParticipates = false,
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest { FullName = "Người đi", Email = "guest@example.com" }
                ]
            },
            "book-for-others-view");
        fixture.Repository.AccessOrder = fixture.Repository.AddedOrder;

        var loaded = await fixture.Service.GetByIdAsync(fixture.Repository.AddedOrder!.OrderId);

        Assert.True(loaded.CanManage);
        Assert.Equal(1_000_000m, loaded.TotalAmount);
        Assert.Single(loaded.Participants);
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
                ContactPhone = DefaultContactPhone,
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
                ContactPhone = DefaultContactPhone,
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
    }

    [Fact]
    public async Task CreateAsync_ReplaysExistingBookingForSameIdempotencyKey()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var existing = CreateExistingOrder(fixture.Repository.Account!, fixture.Repository.Trip!);
        fixture.Repository.ExistingOrder = existing;

        var result = await fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = existing.TripBookingOrder.TripId,
                ContactPhone = DefaultContactPhone
            },
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

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            new CreateTripBookingRequest
            {
                TripId = existing.TripBookingOrder.TripId,
                ContactPhone = DefaultContactPhone,
                BuyerParticipates = false,
                AdditionalParticipants =
                [
                    new CreateBookingParticipantRequest { FullName = "Người khác", Email = "other@example.com" }
                ]
            },
            "same-key"));
    }

    [Fact]
    public async Task GetByIdAsync_UnconfirmedAccountCannotSeeTrip()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        var guest = order.TripBookingOrder.Participants.Single(x => x.Email == "guest@example.com");
        var unconfirmedAccount = new Account
        {
            UserId = Guid.NewGuid(),
            Email = "guest@example.com",
            FullName = "Chưa xác thực",
            Status = "Active",
            EmailConfirmedAt = null
        };
        guest.LinkedUserId = unconfirmedAccount.UserId;
        fixture.Repository.Account = unconfirmedAccount;
        fixture.CurrentUser.RequiredUserId = unconfirmedAccount.UserId;
        fixture.Repository.AccessOrder = order;

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Service.GetByIdAsync(order.OrderId));
    }

    [Fact]
    public async Task GetByIdAsync_ConfirmedCompanionSeesOnlyOwnSeatAndNoMoney()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        var guest = order.TripBookingOrder.Participants.Single(x => x.Email == "guest@example.com");
        var confirmedAccount = new Account
        {
            UserId = Guid.NewGuid(),
            Email = "guest@example.com",
            FullName = "Đã xác thực",
            Status = "Active",
            EmailConfirmedAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        };
        guest.LinkedUserId = confirmedAccount.UserId;
        fixture.Repository.Account = confirmedAccount;
        fixture.CurrentUser.RequiredUserId = confirmedAccount.UserId;
        fixture.Repository.AccessOrder = order;

        var result = await fixture.Service.GetByIdAsync(order.OrderId);

        Assert.False(result.CanManage);
        Assert.Equal(1, result.ParticipantCount);
        Assert.Equal(guest.ParticipantId, Assert.Single(result.Participants).ParticipantId);
        Assert.Equal(0m, result.UnitPrice);
        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(string.Empty, result.PaymentStatus);
        Assert.Null(result.HoldExpiresAt);
    }

    [Fact]
    public async Task UpdateParticipantAsync_ChangingEmailRevokesOldAccountAccessAndRelinksConfirmedNewAccount()
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        var guest = order.TripBookingOrder.Participants.Single(x => x.Email == "guest@example.com");
        var oldAccount = new Account
        {
            UserId = Guid.NewGuid(),
            Email = "guest@example.com",
            FullName = "Người cũ",
            Status = "Active",
            EmailConfirmedAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        };
        var newAccount = new Account
        {
            UserId = Guid.NewGuid(),
            Email = "new@example.com",
            FullName = "Người mới",
            Status = "Active",
            EmailConfirmedAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        };
        guest.LinkedUserId = oldAccount.UserId;
        fixture.Repository.OwnedOrder = order;
        fixture.Repository.AccessOrder = order;
        fixture.Repository.ConfirmedAccounts.Add(newAccount);

        await fixture.Service.UpdateParticipantAsync(
            order.OrderId,
            guest.ParticipantId,
            new UpdateBookingParticipantRequest
            {
                FullName = "Người mới đi thay",
                Email = " NEW@example.com ",
                Phone = "0912345678"
            });

        Assert.Equal(newAccount.UserId, guest.LinkedUserId);
        Assert.Equal("new@example.com", guest.Email);
        Assert.Equal("0912345678", guest.Phone);

        // Tài khoản cũ mất quyền xem
        fixture.Repository.Account = oldAccount;
        fixture.CurrentUser.RequiredUserId = oldAccount.UserId;
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Service.GetByIdAsync(order.OrderId));
    }

    [Fact]
    public async Task GetInviteLinkAsync_AllowsDuringOngoingTripAndExpiresAtTripEnd()
    {
        var ongoingNow = new DateTimeOffset(2026, 12, 1, 5, 0, 0, TimeSpan.Zero);
        var fixture = CreateFixture(totalSlots: 5, utcNow: ongoingNow);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        fixture.Repository.OwnedOrder = order;
        fixture.Repository.SucceededPaymentTotal = order.TotalAmount;

        var link = await fixture.Service.GetInviteLinkAsync(order.OrderId);

        Assert.Equal($"http://localhost:5173/trip-bookings/{order.OrderId}/join#t={TokenOf(order)}", link.Url);
        Assert.Equal(order.TripBookingOrder.Trip.EndAt, link.ExpiresAt);
        Assert.Equal(2, link.TotalSeats);
    }

    [Fact]
    public async Task GetInvitationPreviewAsync_ReturnsNamesOrderedActivitiesAndCurrentMarkerWithoutSensitiveFields()
    {
        // Chuyến bắt đầu ngày 01/12/2026 giờ VN (+07:00), hiện tại là 09:00 giờ VN ngày 2 (02/12/2026 02:00 UTC)
        var vnOffset = TimeSpan.FromHours(7);
        var ongoingNow = new DateTimeOffset(2026, 12, 2, 9, 0, 0, vnOffset);
        var fixture = CreateFixture(totalSlots: 5, utcNow: ongoingNow);
        var trip = fixture.Repository.Trip!;
        trip.StartAt = new DateTimeOffset(2026, 12, 1, 6, 0, 0, vnOffset);
        trip.EndAt = new DateTimeOffset(2026, 12, 2, 18, 0, 0, vnOffset);
        trip.Tour = new Tour
        {
            Name = "Tà Năng 2N1Đ",
            Activities =
            [
                new TourActivity
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 2,
                    DisplayOrder = 2,
                    StartTime = new TimeSpan(8, 30, 0),
                    DurationMinutes = 90,
                    Name = "Đón bình minh đồi cỏ"
                },
                new TourActivity
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 1,
                    DisplayOrder = 1,
                    StartTime = new TimeSpan(7, 0, 0),
                    DurationMinutes = 120,
                    Name = "Xuất phát cửa rừng"
                },
                new TourActivity
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 2,
                    DisplayOrder = 1,
                    StartTime = new TimeSpan(6, 0, 0),
                    DurationMinutes = 60,
                    Name = "Ăn sáng tại trại"
                }
            ]
        };

        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, trip);
        fixture.Repository.InvitationOrder = order;

        var preview = await fixture.Service.GetInvitationPreviewAsync(order.OrderId, TokenOf(order));

        Assert.Equal("Tà Năng 2N1Đ", preview.TourName);
        Assert.Equal("Người mua", preview.BuyerName);
        Assert.Equal(trip.EndAt, preview.ExpiresAt);
        Assert.Equal(["Người mua", "Khách được mời"], preview.ParticipantNames);
        Assert.Equal(
            ["Xuất phát cửa rừng", "Ăn sáng tại trại", "Đón bình minh đồi cỏ"],
            preview.Activities.Select(a => a.Name).ToArray());
        Assert.False(preview.Activities[0].IsCurrent);
        Assert.False(preview.Activities[1].IsCurrent);
        Assert.True(preview.Activities[2].IsCurrent);
    }

    [Theory]
    [InlineData("wrong-token")]
    [InlineData("rotated")]
    [InlineData("unpaid")]
    [InlineData("ended")]
    public async Task GetInvitationPreviewAsync_RejectsInvalidRotatedUnpaidOrEndedLink(string reason)
    {
        var fixture = CreateFixture(totalSlots: 5);
        var order = CreatePaidOrderWithGuest(fixture.Repository.Account!, fixture.Repository.Trip!);
        fixture.Repository.OwnedOrder = order;
        fixture.Repository.InvitationOrder = order;
        fixture.Repository.SucceededPaymentTotal = order.TotalAmount;

        var token = TokenOf(order);
        if (reason == "wrong-token")
            token = "wrong-token";
        if (reason == "rotated")
            await fixture.Service.RotateInviteLinkAsync(order.OrderId);
        if (reason == "unpaid")
            order.PaymentStatus = "Unpaid";
        if (reason == "ended")
            order.TripBookingOrder.Trip.EndAt = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Service.GetInvitationPreviewAsync(order.OrderId, token));
    }

    private static TestFixture CreateFixture(
        int totalSlots,
        int reservedParticipantCount = 0,
        DateTimeOffset? utcNow = null)
    {
        var buyerId = Guid.NewGuid();
        var repository = new FakeTripBookingRepository
        {
            Account = new Account
            {
                UserId = buyerId,
                Email = "buyer@example.com",
                FullName = "Người mua",
                Phone = DefaultContactPhone,
                Status = "Active"
            },
            Trip = new Trip
            {
                TripId = Guid.NewGuid(),
                RefundPolicyId = Guid.NewGuid(),
                PricePerPerson = 1_000_000m,
                TotalSlots = totalSlots,
                Status = "Open",
                StartAt = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                EndAt = new DateTimeOffset(2026, 12, 3, 0, 0, 0, TimeSpan.Zero)
            },
            ReservedParticipantCount = reservedParticipantCount
        };
        var unitOfWork = new FakeUnitOfWork(repository);
        var currentUser = new FakeCurrentUserService(buyerId);
        var service = new TripBookingService(
            unitOfWork,
            currentUser,
            new FixedTimeProvider(utcNow ?? new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)),
            new FakeInviteTokenHelper(),
            Microsoft.Extensions.Options.Options.Create(new EmailSettings
            {
                FrontendBaseUrl = "http://localhost:5173/"
            }),
            TestProxy.Create<INotificationService>(),
            TestProxy.NoRefunds());
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
                    FullName = account.FullName,
                    Email = account.Email,
                    Phone = DefaultContactPhone,
                    Status = "Pending",
                    TripId = trip.TripId,
                    CreatedAt = DateTimeOffset.UnixEpoch
                }
            ]
        };
        var order = new Order
        {
            OrderId = orderId,
            CustomerUserId = account.UserId,
            CustomerUser = account,
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
        order.TripBookingOrder.Participants.Add(new BookingParticipant
        {
            ParticipantId = Guid.NewGuid(),
            BookingOrderId = order.OrderId,
            FullName = "Khách được mời",
            Email = "guest@example.com",
            Status = "Pending",
            TripId = trip.TripId,
            CreatedAt = DateTimeOffset.UnixEpoch.AddTicks(1)
        });
        order.TripBookingOrder.ParticipantCount = 2;
        order.Status = "Confirmed";
        order.PaymentStatus = "Paid";
        order.TotalAmount = trip.PricePerPerson * 2;
        order.SubtotalAmount = order.TotalAmount;
        return order;
    }

    private static string TokenOf(Order order)
        => new FakeInviteTokenHelper().CreateGroupToken(order.OrderId, order.TripBookingOrder.InviteVersion);

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
        public string CreateGroupToken(Guid orderId, int version) => $"token:{orderId:N}:{version}";
        public string CreateEmailVerificationToken(Guid userId, string normalizedEmail, DateTimeOffset expiresAt) => throw new NotSupportedException();
        public bool TryParseEmailVerificationToken(string? token, out Guid userId, out DateTimeOffset expiresAt) => throw new NotSupportedException();
        public bool VerifyEmailVerificationToken(string? token, Guid userId, string normalizedEmail, DateTimeOffset utcNow) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork(ITripBookingRepository repository) : IUnitOfWork
    {
        public IAdminOperationRepository AdminOperations => throw new NotSupportedException();
        public ITripBookingRepository TripBookings { get; } = repository;
        public IAccountRepository Accounts => throw new NotSupportedException();
        public IRoleRepository Roles => throw new NotSupportedException();
        public IRefreshTokenRepository RefreshTokens => throw new NotSupportedException();
        public IAccessTokenRepository AccessTokens => throw new NotSupportedException();
        public ITourRepository Tours { get; } = new ActiveTourRepositoryStub();
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
        public Task<bool> TrySaveChangesIgnoringUniqueViolationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTripBookingRepository : ITripBookingRepository
    {
        public Account? Account { get; set; }
        public List<Account> ConfirmedAccounts { get; } = [];
        public Trip? Trip { get; init; }
        public int ReservedParticipantCount { get; init; }
        public Order? ExistingOrder { get; set; }
        public Order? AccessOrder { get; set; }
        public Order? OwnedOrder { get; set; }
        public Order? InvitationOrder { get; set; }
        public decimal SucceededPaymentTotal { get; set; }
        public Order? AddedOrder { get; private set; }
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

        public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            AddedOrder = order;
            return Task.CompletedTask;
        }

        public Task<Account?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Account?.UserId == userId ? Account : null);

        public Task<IReadOnlyList<Account>> GetConfirmedAccountsByEmailsAsync(
            IReadOnlyCollection<string> normalizedEmails,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Account>>(ConfirmedAccounts
                .Where(a => a.EmailConfirmedAt is not null && normalizedEmails.Contains(a.Email.ToLowerInvariant()))
                .ToList());

        public Task<IReadOnlyList<BookingParticipant>> GetUnlinkedPaidParticipantsByEmailAsync(
            string normalizedEmail,
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Order?> GetBookingAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(InvitationOrder);

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
