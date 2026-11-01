using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class PaymentExpiryRaceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Expiry_ReleasesRentalAssetExactlyOnce()
    {
        var seed = await fixture.SeedOrderAsync(); Guid assignmentId;
        await using (var db = fixture.CreateDb())
        {
            var product = new Product { Category = new Category { Name = "Test equipment", CategoryType = "PRODUCT" }, CreatedByUserId = seed.StaffId, Name = "Test tent" };
            var variant = new ProductVariant { Product = product, Sku = Guid.NewGuid().ToString("N"), Name = "Tent" };
            var item = new RentalOrderItem { RentalOrderItemId = Guid.NewGuid(), RentalOrderId = seed.OrderId, ProductVariant = variant, Quantity = 1, BillableDays = 1 };
            var assignment = new AssetAssignment { AssetAssignmentId = Guid.NewGuid(), RentalOrderItem = item, EquipmentAsset = new EquipmentAsset { EquipmentAssetId = Guid.NewGuid(), ProductVariant = variant, AssetCode = Guid.NewGuid().ToString("N"), AssetStatus = "Available" }, Status = "Reserved", AssignedAt = TestClock.Now.UtcDateTime, ReservedFrom = TestClock.Now.UtcDateTime.AddDays(3), ReservedUntil = TestClock.Now.UtcDateTime.AddDays(4) };
            assignmentId = assignment.AssetAssignmentId; db.Add(assignment); await db.SaveChangesAsync();
        }
        async Task Expire() { await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db); await new OrderPaymentLifecycleService(uow, new TestClock { Current = TestClock.Now.AddMinutes(15) }).ExpireAsync(seed.OrderId); }
        await Task.WhenAll(Expire(), Expire());
        await using var verify = fixture.CreateDb();
        Assert.Equal("Expired", (await verify.Orders.FindAsync(seed.OrderId))!.Status);
        Assert.Equal("Cancelled", (await verify.Set<AssetAssignment>().FindAsync(assignmentId))!.Status);
        var audit = Assert.Single(await verify.AuditLogs.Where(x => x.EntityId == seed.OrderId && x.Action == "Order.Expired").ToListAsync());
        Assert.Equal("Order", audit.EntityType);
    }

    [Fact]
    public async Task PartialMoneyAfterExpiry_IsVisibleInReconciliationQueue()
    {
        var seed = await fixture.SeedOrderAsync(400_000); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 100_000));
        await using (var db = fixture.CreateDb()) { using var uow = new UnitOfWork(db); await new OrderPaymentLifecycleService(uow, new TestClock { Current = TestClock.Now.AddMinutes(15) }).ExpireAsync(seed.OrderId); }
        await using var verify = fixture.CreateDb();
        Assert.Equal("NeedsReconciliation", (await verify.Set<BankTransaction>().SingleAsync(x => x.OrderId == seed.OrderId)).Status);
    }

    [Fact]
    public async Task WebhookAtDeadline_AndExpiryCannotConfirmAndRelease()
    {
        var seed = await fixture.SeedOrderAsync(); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        var deadline = new TestClock { Current = TestClock.Now.AddMinutes(15) };
        async Task Expire() { await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db); await new OrderPaymentLifecycleService(uow, deadline).ExpireAsync(seed.OrderId); }
        await Task.WhenAll(Expire(), hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 1_000_000), deadline));
        await using var verify = fixture.CreateDb();
        var order = await verify.Orders.FindAsync(seed.OrderId);
        Assert.Equal("Expired", order!.Status); Assert.Equal("NeedsReconciliation", order.PaymentStatus);
        Assert.Equal(1_000_000m, await verify.Set<BankTransaction>().Where(x => x.OrderId == seed.OrderId).SumAsync(x => x.Amount));
    }
}
