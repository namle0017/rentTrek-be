using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.BLL.DTOs.Auth.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RegisterAsync_LeavesEmailUnconfirmedAndSendsVerificationLink()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.RegisterAsync(new RegisterRequest
        {
            Email = " Guest@Example.com ",
            Password = "Password@123",
            FullName = "Khách mới",
            Phone = "0901234567"
        });

        Assert.False(response.User.EmailConfirmed);
        var account = Assert.Single(fixture.Accounts.Accounts);
        Assert.Null(account.EmailConfirmedAt);
        var sent = Assert.Single(fixture.EmailSender.Sent);
        Assert.Equal("guest@example.com", sent.To);
        Assert.Contains("http://localhost:5173/verify-email#t=", sent.Body);
    }

    [Fact]
    public async Task LoginAsync_LocksEmailAfterTooManyWrongPasswords()
    {
        var fixture = CreateFixture();
        fixture.Accounts.Accounts.Add(new Account
        {
            UserId = Guid.NewGuid(),
            Email = "victim@example.com",
            PasswordHash = "hashed:Correct@123",
            Status = "Active",
            FullName = "Nạn nhân"
        });

        for (var i = 0; i < AuthAttemptLimiter.MaxFailures; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => fixture.Service.LoginAsync(
                new LoginRequest { Email = "victim@example.com", Password = "wrong" }));
        }

        // Đúng mật khẩu vẫn bị chặn khi đang khóa.
        await Assert.ThrowsAsync<TooManyRequestsException>(() => fixture.Service.LoginAsync(
            new LoginRequest { Email = " Victim@Example.com ", Password = "Correct@123" }));
    }

    [Fact]
    public async Task GoogleLoginAsync_RejectsUnsignedTokenWhenDevTokensDisabled()
    {
        var fixture = CreateFixture();
        // JWT không chữ ký (alg=none) tự khai email admin.
        static string B64(string s) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var forged = $"{B64("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{B64("{\"sub\":\"1\",\"email\":\"admin@rentrek.com\"}")}.";

        await Assert.ThrowsAsync<UnauthorizedException>(() => fixture.Service.GoogleLoginAsync(
            new GoogleLoginRequest { IdToken = forged }));
        Assert.Empty(fixture.Accounts.Accounts);
    }

    [Fact]
    public async Task VerifyEmailAsync_ConfirmsEmailAndLinksOnlyPaidFutureTrips()
    {
        var fixture = CreateFixture();
        var account = new Account
        {
            UserId = Guid.NewGuid(),
            Email = "friend@example.com",
            FullName = "Bạn đồng hành",
            Status = "Active"
        };
        fixture.Accounts.Accounts.Add(account);

        var paidFutureSeat = SeatFor("friend@example.com", "Confirmed", "Paid", Now.AddDays(5));
        var unpaidFutureSeat = SeatFor("friend@example.com", "PendingPayment", "Unpaid", Now.AddDays(5));
        var paidStartedSeat = SeatFor("friend@example.com", "Confirmed", "Paid", Now.AddHours(-1));
        fixture.TripBookings.Seats.AddRange([paidFutureSeat, unpaidFutureSeat, paidStartedSeat]);

        var token = fixture.TokenHelper.CreateEmailVerificationToken(account.UserId, "friend@example.com", Now.AddHours(24));

        await fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Token = token });

        Assert.Equal(Now.UtcDateTime, account.EmailConfirmedAt);
        Assert.Equal(account.UserId, paidFutureSeat.LinkedUserId);
        Assert.Null(unpaidFutureSeat.LinkedUserId);
        Assert.Null(paidStartedSeat.LinkedUserId);
    }

    [Fact]
    public async Task VerifyEmailAsync_RejectsInvalidOrExpiredToken()
    {
        var fixture = CreateFixture();
        var account = new Account
        {
            UserId = Guid.NewGuid(),
            Email = "friend@example.com",
            FullName = "Bạn đồng hành",
            Status = "Active"
        };
        fixture.Accounts.Accounts.Add(account);
        var expiredToken = fixture.TokenHelper.CreateEmailVerificationToken(account.UserId, "friend@example.com", Now.AddMinutes(-1));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Token = "bad-token" }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Token = expiredToken }));
        Assert.Null(account.EmailConfirmedAt);
    }

    private static BookingParticipant SeatFor(string email, string orderStatus, string paymentStatus, DateTimeOffset tripStartAt)
    {
        var orderId = Guid.NewGuid();
        var trip = new Trip { TripId = Guid.NewGuid(), StartAt = tripStartAt, EndAt = tripStartAt.AddDays(2) };
        var order = new Order { OrderId = orderId, Status = orderStatus, PaymentStatus = paymentStatus };
        var booking = new TripBookingOrder { OrderId = orderId, Order = order, Trip = trip };
        order.TripBookingOrder = booking;
        return new BookingParticipant
        {
            ParticipantId = Guid.NewGuid(),
            BookingOrderId = orderId,
            BookingOrder = booking,
            TripId = trip.TripId,
            Trip = trip,
            FullName = "Người đi",
            Email = email
        };
    }

    private static Fixture CreateFixture()
    {
        var accounts = new FakeAccountRepository();
        var tripBookings = new FakeTripBookingRepository();
        var tokenHelper = new InviteTokenHelper(Microsoft.Extensions.Options.Options.Create(new InviteSettings { SigningKey = new string('s', 32) }));
        var emailSender = new FakeEmailSender();
        var service = new AuthService(
            new FakeUnitOfWork(accounts, tripBookings),
            new FakePasswordHasher(),
            new FakeJwtHelper(),
            Microsoft.Extensions.Options.Options.Create(new JwtSettings { RefreshTokenExpirationDays = 7 }),
            new FixedTimeProvider(Now),
            tokenHelper,
            emailSender,
            Microsoft.Extensions.Options.Options.Create(new EmailSettings
            {
                Username = "rentrek@gmail.com",
                Password = "app-password",
                FrontendBaseUrl = "http://localhost:5173"
            }),
            NullLogger<AuthService>.Instance,
            new EmailService(NullLogger<EmailService>.Instance),
            new AuthAttemptLimiter(new InMemoryAuthAttempts(), new FixedTimeProvider(Now)),
            Microsoft.Extensions.Options.Options.Create(new GoogleAuthSettings()));
        return new Fixture(service, accounts, tripBookings, tokenHelper, emailSender);
    }

    private sealed record Fixture(
        AuthService Service,
        FakeAccountRepository Accounts,
        FakeTripBookingRepository TripBookings,
        InviteTokenHelper TokenHelper,
        FakeEmailSender EmailSender);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private sealed class FakeJwtHelper : IJwtHelper
    {
        public (string Token, string Jti, DateTime ExpiresAt) GenerateAccessToken(Account account)
            => ("access", Guid.NewGuid().ToString("N"), Now.UtcDateTime.AddMinutes(30));

        public string GenerateRefreshToken() => "refresh";
        public string HashToken(string token) => $"hash:{token}";
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Body)> Sent { get; } = [];

        public Task SendAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Sent.Add((toAddress, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public List<Account> Accounts { get; } = [];

        public Task<Account?> GetByEmailWithRoleAsync(string email, bool tracking = false, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.FirstOrDefault(a => a.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task<Account?> GetByIdWithRoleAsync(Guid userId, bool tracking = false, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.FirstOrDefault(a => a.UserId == userId));

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.Any(a => a.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Account entity, CancellationToken cancellationToken = default)
        {
            Accounts.Add(entity);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<AdminUserRow> Items, int TotalCount)> GetAdminPagedAsync(AdminUserFilter filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserRow?> GetAdminRowAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AccountActivityRow> GetActivityAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Account?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Account> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(Account entity) { }
        public void Remove(Account entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Account> entities) => throw new NotSupportedException();
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByNameAsync(string roleName, bool tracking = false, CancellationToken cancellationToken = default)
            => Task.FromResult<Role?>(new Role { RoleId = Guid.NewGuid(), RoleName = roleName });

        public Task<Role?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Role entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Role> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(Role entity) => throw new NotSupportedException();
        public void Remove(Role entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Role> entities) => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public Task AddAsync(RefreshToken entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, bool tracking = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RefreshToken?> GetByTokenHashWithAccessTokensAsync(string tokenHash, bool tracking = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<RefreshToken>> GetOtherActiveTokensAsync(Guid userId, Guid? excludeFamilyId, bool tracking = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeFamilyTokensAsync(Guid familyId, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RefreshToken?> GetForUserAsync(Guid userId, Guid refreshTokenId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<RefreshToken>> GetByUserWithAccessTokensAsync(Guid userId, Guid? familyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RefreshToken?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<RefreshToken> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(RefreshToken entity) => throw new NotSupportedException();
        public void Remove(RefreshToken entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<RefreshToken> entities) => throw new NotSupportedException();
    }

    private sealed class FakeAccessTokenRepository : IAccessTokenRepository
    {
        public Task AddAsync(AccessToken entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsJtiRevokedAsync(string jti, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<AccessToken?> GetByJtiWithRefreshTokenAsync(string jti, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsSessionActiveAsync(Guid userId, string jti, string role, DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AccessToken?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<AccessToken> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(AccessToken entity) => throw new NotSupportedException();
        public void Remove(AccessToken entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<AccessToken> entities) => throw new NotSupportedException();
    }

    private sealed class FakeTripBookingRepository : ITripBookingRepository
    {
        public List<BookingParticipant> Seats { get; } = [];

        // Trả tất cả ghế trùng email để chứng minh AuthService tự loại đơn chưa Paid hoặc đã khởi hành
        public Task<IReadOnlyList<BookingParticipant>> GetUnlinkedPaidParticipantsByEmailAsync(
            string normalizedEmail,
            DateTimeOffset utcNow,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BookingParticipant>>(
                Seats.Where(s => s.Email?.ToLowerInvariant() == normalizedEmail).ToList());

        public Task AcquireIdempotencyLockAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AcquireBookingLockAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Account?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Account>> GetConfirmedAccountsByEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> GetReservedParticipantCountAsync(Guid tripId, DateTimeOffset utcNow, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Order?> GetByIdempotencyKeyAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Order?> GetForAccessAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Order?> GetOwnedBookingAsync(Guid orderId, Guid ownerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal> GetSucceededPaymentTotalAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Order?> GetBookingAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork(IAccountRepository accounts, ITripBookingRepository tripBookings) : IUnitOfWork
    {
        public IAdminOperationRepository AdminOperations => throw new NotSupportedException();
        public IAccountRepository Accounts { get; } = accounts;
        public ITripBookingRepository TripBookings { get; } = tripBookings;
        public IRoleRepository Roles { get; } = new FakeRoleRepository();
        public IRefreshTokenRepository RefreshTokens { get; } = new FakeRefreshTokenRepository();
        public IAccessTokenRepository AccessTokens { get; } = new FakeAccessTokenRepository();
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
        public IRefundCaseRepository RefundCases => throw new NotSupportedException();
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

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<bool> TrySaveChangesIgnoringUniqueViolationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
