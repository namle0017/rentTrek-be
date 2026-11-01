using Microsoft.EntityFrameworkCore;
using Npgsql;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using Testcontainers.PostgreSql;

namespace RenTrek.IntegrationTests;

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    public AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(container.GetConnectionString()).Options);
    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var script = db.Database.GenerateCreateScript();
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "generated-schema.sql"), script);
    }
    public async Task DisposeAsync() => await container.DisposeAsync();

    public async Task<Seed> SeedOrderAsync(decimal total = 1_000_000m)
    {
        await using var db = CreateDb();
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Customer" };
        var staffRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
        var customer = new Account { UserId = Guid.NewGuid(), Role = customerRole, Email = $"{Guid.NewGuid():N}@example.test", FullName = "Customer" };
        var staff = new Account { UserId = Guid.NewGuid(), Role = staffRole, Email = $"{Guid.NewGuid():N}@example.test", FullName = "Staff" };
        // Roles are shared by name in the real schema.
        customer.Role = await db.Roles.FirstOrDefaultAsync(x => x.RoleName == "Customer") ?? customerRole;
        staff.Role = await db.Roles.FirstOrDefaultAsync(x => x.RoleName == "Staff") ?? staffRole;
        var policy = new RefundPolicy { RefundPolicyId = Guid.NewGuid(), CreatedByUser = staff, PolicyType = "Rental", Name = "Test policy", Status = "Active", Version = 1, PolicyFamilyId = Guid.NewGuid(), EffectiveFrom = DateTime.UtcNow.AddDays(-1) };
        var rule = new RefundPolicyRule { RefundPolicyRuleId = Guid.NewGuid(), RefundPolicy = policy, MinHoursBefore = 24, RefundPercentage = 50m };
        var order = new Order { OrderId = Guid.NewGuid(), CustomerUser = customer, RefundPolicy = policy, OrderCode = $"TEST-{Guid.NewGuid():N}", OrderType = "Rental", TotalAmount = total, SubtotalAmount = total, Status = "PendingPayment", PaymentStatus = "Unpaid", HoldExpiresAt = TestClock.Now.AddMinutes(15), CreatedAt = TestClock.Now, UpdatedAt = TestClock.Now };
        var rental = new RentalOrder { Order = order, RentalStartAt = TestClock.Now.UtcDateTime.AddDays(3), RentalEndAt = TestClock.Now.UtcDateTime.AddDays(4) };
        db.AddRange(customer, staff, rule, rental);
        await db.SaveChangesAsync();
        return new Seed(order.OrderId, customer.UserId, staff.UserId, policy.RefundPolicyId, rule.RefundPolicyRuleId);
    }
}

public sealed record Seed(Guid OrderId, Guid CustomerId, Guid StaffId, Guid PolicyId, Guid RuleId);
public sealed class TestClock : TimeProvider
{
    public static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset Current { get; set; } = Now;
    public override DateTimeOffset GetUtcNow() => Current;
}
