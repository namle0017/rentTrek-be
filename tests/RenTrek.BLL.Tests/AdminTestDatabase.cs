using Microsoft.EntityFrameworkCore;
using Npgsql;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

internal sealed class AdminTestDatabase : IAsyncDisposable
{
    private readonly string _serverConnection;
    public string ConnectionString { get; }
    public string Name { get; } = "rentrek_admin_test_" + Guid.NewGuid().ToString("N");
    public Guid AdminId { get; } = Guid.NewGuid();
    public Guid StaffId { get; } = Guid.NewGuid();
    public Guid OtherStaffId { get; } = Guid.NewGuid();
    public Guid CustomerId { get; } = Guid.NewGuid();
    public Guid GuideId { get; } = Guid.NewGuid();
    public Guid TripId { get; } = Guid.NewGuid();
    public Guid VariantId { get; } = Guid.NewGuid();
    public Guid ParticipantId { get; } = Guid.NewGuid();
    public Guid ReviewId { get; } = Guid.NewGuid();
    public Guid PolicyId { get; } = Guid.NewGuid();
    public DateTimeOffset Now { get; } = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    public AdminTestDatabase()
    {
        _serverConnection = Environment.GetEnvironmentVariable("RENTREK_TEST_POSTGRES")!;
        ConnectionString = new NpgsqlConnectionStringBuilder(_serverConnection) { Database = Name, Pooling = false }.ConnectionString;
    }

    public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
    public AdminServices Service(AppDbContext context, Guid? actorId = null, string role = "Admin")
    {
        var (uow, actor, clock) = (new UnitOfWork(context), new Actor(actorId ?? AdminId, role), new Clock(Now));
        var notifications = new NotificationService(uow, actor, clock);
        return new(new(uow, actor, new PasswordHasher(), clock), new(uow, actor, clock), new(uow, actor, clock, notifications),
            new(uow, actor, clock), new(uow, actor, clock), new(uow, actor, clock));
    }

    public sealed record AdminServices(AdminUserService Users, AdminMasterDataService MasterData, AdminOrderService Orders,
        AdminPromotionService Promotions, AdminGovernanceService Governance, AdminReportService Reports);
    public RentalFulfillmentService Fulfillment(AppDbContext context, TimeProvider clock, Guid? actorId = null, string role = "Admin")
        => new(new UnitOfWork(context), new Actor(actorId ?? AdminId, role), clock);

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_serverConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {Name}", connection);
        await command.ExecuteNonQueryAsync();
        await using var context = Context();
        await context.Database.EnsureCreatedAsync();
        var roles = new[] { "Admin", "Staff", "Customer", "Guide" }.Select(name => new Role { RoleName = name }).ToList();
        context.Roles.AddRange(roles);
        foreach (var (id, role) in new[] { (AdminId, "Admin"), (StaffId, "Staff"), (OtherStaffId, "Staff"), (CustomerId, "Customer"), (GuideId, "Guide") })
            context.Accounts.Add(new Account { UserId = id, Role = roles.Single(x => x.RoleName == role), Email = id + "@test.invalid", FullName = role + " Test", PasswordHash = new PasswordHasher().Hash("TestPassword123!"), CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime });
        var category = new Category { Name = "Trekking", CategoryType = "TOUR" };
        var location = new Location { Name = "Bidoup", Address = "Lâm Đồng" };
        var tour = new Tour { Name = "Bidoup Test", Category = category, MainLocation = location, CreatedByUserId = AdminId };
        var policy = new RefundPolicy
        {
            RefundPolicyId = PolicyId,
            Name = "Test policy",
            CreatedByUserId = AdminId,
            PolicyFamilyId = Guid.NewGuid(),
            Version = 1,
            PolicyType = "Tour",
            Status = "Active",
            EffectiveFrom = Now.UtcDateTime.AddYears(-1),
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime
        };
        context.RefundPolicies.Add(policy); context.Tours.Add(tour);
        context.Trips.Add(new Trip
        {
            TripId = TripId,
            Tour = tour,
            RefundPolicy = policy,
            ManagedByUserId = StaffId,
            StartAt = Now.AddDays(2),
            EndAt = Now.AddDays(3),
            TotalSlots = 10,
            MinParticipants = 3,
            Status = "Open",
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await context.SaveChangesAsync();
        context.Orders.Add(new Order
        {
            OrderId = Guid.NewGuid(),
            OrderCode = "TOUR-TEST",
            CustomerUserId = CustomerId,
            OrderType = "TripBooking",
            TotalAmount = 1000000,
            SubtotalAmount = 1000000,
            Status = "PendingPayment",
            PaymentStatus = "Unpaid",
            CreatedAt = Now,
            UpdatedAt = Now,
            HoldExpiresAt = Now.AddMinutes(15),
            RefundPolicyId = PolicyId,
            TripBookingOrder = new TripBookingOrder { TripId = TripId, ParticipantCount = 1, GroupingPreference = "Together" }
        });
        var product = new Product { Name = "Lều Test", CategoryId = category.Id, CreatedByUserId = AdminId };
        var variant = new ProductVariant { Id = VariantId, Product = product, Sku = "TENT-TEST", Name = "Lều 2 người" };
        context.ProductVariants.Add(variant);
        context.Orders.Add(new Order
        {
            OrderId = Guid.NewGuid(),
            OrderCode = "RENT-TEST",
            CustomerUserId = CustomerId,
            OrderType = "Rental",
            TotalAmount = 200000,
            SubtotalAmount = 200000,
            Status = "Confirmed",
            PaymentStatus = "Paid",
            CreatedAt = Now,
            UpdatedAt = Now,
            RefundPolicyId = PolicyId,
            RentalOrder = new RentalOrder
            {
                RentalStartAt = Now.UtcDateTime.AddDays(-2),
                RentalEndAt = Now.UtcDateTime.AddDays(-1),
                RentalOrderItems = [new RentalOrderItem { RentalOrderItemId = Guid.NewGuid(), ProductVariant = variant, Quantity = 1, BillableDays = 1, DailyPrice = 200000, LineAmount = 200000 }]
            }
        });
        await context.SaveChangesAsync();
        var tourOrder = await context.Orders.SingleAsync(x => x.OrderCode == "TOUR-TEST");
        context.BookingParticipants.Add(new BookingParticipant
        {
            ParticipantId = ParticipantId,
            BookingOrderId = tourOrder.OrderId,
            TripId = TripId,
            FullName = "Khách Test",
            Email = "guest@test.invalid",
            Status = "Active",
            CreatedAt = Now,
            UpdatedAt = Now
        });
        context.Reviews.Add(new Review { ReviewId = ReviewId, ParticipantId = ParticipantId, Rating = 4, Comment = "Nội dung gốc", CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime });
        context.SystemConfigurations.Add(new SystemConfiguration { ConfigurationId = Guid.NewGuid(), ConfigKey = "orders.holdExpiresMinutes", ConfigValue = "15", ValueType = "int", UpdatedByUserId = AdminId, CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime });
        var rentalItem = await context.RentalOrderItems.SingleAsync();
        var asset = new EquipmentAsset { EquipmentAssetId = Guid.NewGuid(), ProductVariantId = VariantId, AssetCode = "TENT-001", AssetStatus = "Available", CreatedAt = Now.UtcDateTime, UpdatedAt = Now.UtcDateTime };
        context.AssetAssignments.Add(new AssetAssignment
        {
            AssetAssignmentId = Guid.NewGuid(),
            EquipmentAsset = asset,
            RentalOrderItemId = rentalItem.RentalOrderItemId,
            Status = "Reserved",
            AssignedAt = Now.UtcDateTime.AddDays(-2),
            ReservedFrom = Now.UtcDateTime.AddDays(-2),
            ReservedUntil = Now.UtcDateTime.AddDays(-1),
            CreatedAt = Now.UtcDateTime
        });
        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_serverConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {Name} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Actor(Guid userId, string role) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid RequiredUserId => userId;
        public string? Jti => null;
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
