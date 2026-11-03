using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class PaymentServiceTests
{
    private const string ApiKey = "test-api-key";
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_ReturnsTransferCodeAndReusesPendingPayment()
    {
        var fixture = CreateFixture();

        var (first, created) = await fixture.Service.CreateAsync(
            fixture.Order.CustomerUserId, fixture.Order.OrderId, new CreatePaymentRequest { PaymentMethod = "BankTransfer" });
        var (second, createdAgain) = await fixture.Service.CreateAsync(
            fixture.Order.CustomerUserId, fixture.Order.OrderId, new CreatePaymentRequest { PaymentMethod = "BankTransfer" });

        Assert.True(created);
        Assert.False(createdAgain);
        Assert.Equal(first.PaymentId, second.PaymentId);
        Assert.Equal(1_000_000m, first.Amount);
        Assert.Matches("^RTP[0-9A-F]{16}$", first.BankTransfer.TransferContent);
        Assert.Contains($"des={first.BankTransfer.TransferContent}", first.BankTransfer.QrImageUrl);
        Assert.Single(fixture.Repository.Payments);
    }

    [Fact]
    public async Task CreateAsync_RejectsExpiredHold()
    {
        var fixture = CreateFixture();
        fixture.Order.HoldExpiresAt = Now.AddMinutes(-1);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            fixture.Order.CustomerUserId, fixture.Order.OrderId, new CreatePaymentRequest { PaymentMethod = "BankTransfer" }));

        Assert.Equal("HOLD_EXPIRED", exception.ErrorCode);
    }

    [Fact]
    public async Task Webhook_ConfirmsOrderWhenAmountMatches()
    {
        var fixture = CreateFixture();
        var transferContent = await CreatePaymentAsync(fixture);

        await fixture.Service.HandleSePayWebhookAsync($"Apikey {ApiKey}", Webhook(1, $"CT {transferContent} chuyen tien", 1_000_000m));

        var payment = Assert.Single(fixture.Repository.Payments);
        Assert.Equal("Succeeded", payment.Status);
        Assert.Equal("SEPAY:1", payment.ProviderTransactionId);
        Assert.Equal(Now, payment.ProcessedAt);
        Assert.Equal("Confirmed", fixture.Order.Status);
        Assert.Equal("Paid", fixture.Order.PaymentStatus);
        Assert.True(fixture.Repository.LockAcquired);
    }

    [Fact]
    public async Task Webhook_IgnoresDuplicateTransaction()
    {
        var fixture = CreateFixture();
        var transferContent = await CreatePaymentAsync(fixture);
        var request = Webhook(7, transferContent, 1_000_000m);

        await fixture.Service.HandleSePayWebhookAsync($"Apikey {ApiKey}", request);
        await fixture.Service.HandleSePayWebhookAsync($"Apikey {ApiKey}", request);

        Assert.Single(fixture.Repository.Payments);
        Assert.Equal("Paid", fixture.Order.PaymentStatus);
    }

    [Fact]
    public async Task Webhook_FlagsReconciliationWhenAmountDiffers()
    {
        var fixture = CreateFixture();
        var transferContent = await CreatePaymentAsync(fixture);

        await fixture.Service.HandleSePayWebhookAsync($"Apikey {ApiKey}", Webhook(2, transferContent, 500_000m));

        Assert.Equal(2, fixture.Repository.Payments.Count);
        Assert.All(fixture.Repository.Payments, payment => Assert.Equal("Pending", payment.Status));
        Assert.Equal("PendingPayment", fixture.Order.Status);
        Assert.Equal("NeedsReconciliation", fixture.Order.PaymentStatus);
    }

    [Fact]
    public async Task Webhook_FlagsReconciliationWhenHoldExpired()
    {
        var fixture = CreateFixture();
        var transferContent = await CreatePaymentAsync(fixture);
        fixture.Order.HoldExpiresAt = Now.AddMinutes(-1);

        await fixture.Service.HandleSePayWebhookAsync($"Apikey {ApiKey}", Webhook(3, transferContent, 1_000_000m));

        Assert.Equal("PendingPayment", fixture.Order.Status);
        Assert.Equal("NeedsReconciliation", fixture.Order.PaymentStatus);
    }

    [Fact]
    public async Task Webhook_FlagsReconciliationWhenPaymentArrivesAfterCancellation()
    {
        var fixture = CreateFixture();
        var transferContent = await CreatePaymentAsync(fixture);
        fixture.Order.Status = "Cancelled";
        fixture.Order.PaymentStatus = "Unpaid";
        fixture.Repository.Payments.Single().Status = "Cancelled";

        await fixture.Service.HandleSePayWebhookAsync($"Apikey {ApiKey}", Webhook(8, transferContent, 1_000_000m));

        Assert.Equal("Cancelled", fixture.Order.Status);
        Assert.Equal("NeedsReconciliation", fixture.Order.PaymentStatus);
        Assert.Contains(fixture.Repository.Payments, payment => payment.ProviderTransactionId == "SEPAY:8");
        Assert.True(fixture.Repository.LockAcquired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Apikey wrong-key")]
    [InlineData("Bearer test-api-key")]
    public async Task Webhook_RejectsInvalidApiKey(string? authorization)
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            fixture.Service.HandleSePayWebhookAsync(authorization, Webhook(4, "RTP0000000000000000", 1m)));
    }

    private static async Task<string> CreatePaymentAsync(Fixture fixture)
    {
        var (payment, _) = await fixture.Service.CreateAsync(
            fixture.Order.CustomerUserId, fixture.Order.OrderId, new CreatePaymentRequest { PaymentMethod = "BankTransfer" });
        return payment.BankTransfer.TransferContent;
    }

    private static SePayWebhookRequest Webhook(long id, string content, decimal amount) => new()
    {
        Id = id,
        Content = content,
        TransferType = "in",
        TransferAmount = amount
    };

    private static Fixture CreateFixture()
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerUserId = Guid.NewGuid(),
            OrderType = "TripBooking",
            OrderCode = "RT-20261028-TEST",
            TotalAmount = 1_000_000m,
            Status = "PendingPayment",
            PaymentStatus = "Unpaid",
            HoldExpiresAt = Now.AddMinutes(15)
        };
        var repository = new FakePaymentRepository(order);
        var service = new PaymentService(
            new FakeUnitOfWork(repository),
            new FixedTimeProvider(Now),
            Microsoft.Extensions.Options.Options.Create(new SePaySettings
            {
                Api = new SePaySettings.ApiSettings { Key = ApiKey },
                BankName = "MBBank",
                AccountNumber = "0123456789",
                AccountName = "RENTREK"
            }),
            NullLogger<PaymentService>.Instance);
        return new Fixture(service, repository, order);
    }

    private sealed record Fixture(PaymentService Service, FakePaymentRepository Repository, Order Order);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakePaymentRepository(Order order) : IPaymentRepository
    {
        public List<Payment> Payments { get; } = [];
        public bool LockAcquired { get; private set; }

        public Task AcquireOrderLockAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            LockAcquired = true;
            return Task.CompletedTask;
        }

        public Task<Order?> GetOrderAsync(Guid orderId, Guid? customerUserId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<Order?>(
                order.OrderId == orderId && (customerUserId is null || order.CustomerUserId == customerUserId) ? order : null);

        public Task<Payment?> GetLatestPendingCollectionAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Payments.LastOrDefault(p => p.OrderId == orderId && p.PaymentType == "Collection" && p.Status == "Pending"));

        public Task<Payment?> GetOwnedAsync(Guid paymentId, Guid orderId, Guid customerUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(Payments.SingleOrDefault(p => p.PaymentId == paymentId));

        public Task<Payment?> FindByIdPrefixAsync(string paymentIdPrefix, CancellationToken cancellationToken = default)
            => Task.FromResult(Payments.FirstOrDefault(p => p.PaymentId.ToString("N")[..16] == paymentIdPrefix));

        public Task<bool> ProviderTransactionExistsAsync(string providerTransactionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Payments.Any(p => p.ProviderTransactionId == providerTransactionId));

        public Task<Payment?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
            => Task.FromResult(Payments.SingleOrDefault(p => p.PaymentId == (Guid)id));

        public Task AddAsync(Payment entity, CancellationToken cancellationToken = default)
        {
            Payments.Add(entity);
            return Task.CompletedTask;
        }

        public IQueryable<Payment> Query() => throw new NotSupportedException();
        public IQueryable<Payment> QueryTracking() => throw new NotSupportedException();
        public Task<bool> AnyAsync(Expression<Func<Payment, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Payment> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(Payment entity) => throw new NotSupportedException();
        public void Remove(Payment entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Payment> entities) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork(IPaymentRepository payments) : IUnitOfWork
    {
        public IPaymentRepository Payments { get; } = payments;
        public ITripBookingRepository TripBookings => throw new NotSupportedException();
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

        public void Dispose()
        {
        }
    }
}
