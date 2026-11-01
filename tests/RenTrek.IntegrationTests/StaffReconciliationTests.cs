using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class StaffReconciliationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task StaffCanMatchVerifiedUnmatchedMoney_WithoutDuplicateCredit()
    {
        var seed = await fixture.SeedOrderAsync(); var hooks = new WebhookInboxTests(fixture); await hooks.Intent(seed);
        var receipt = await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), "WRONG-CODE", 1_000_000));
        Guid txId, paymentId;
        await using (var db = fixture.CreateDb()) { txId = (await db.Set<BankTransaction>().SingleAsync(x => x.ReceiptId == receipt)).BankTransactionId; paymentId = (await db.Payments.SingleAsync(x => x.OrderId == seed.OrderId)).PaymentId; }
        async Task Match()
        {
            await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db); var clock = new TestClock();
            var service = new StaffReconciliationService(uow, new TestUser(seed.StaffId, "Staff"), clock, new TestProtector(), WebhookInboxTests.CreateWebhookService(uow, clock));
            await service.MatchAsync(txId, paymentId, "Đã kiểm tra giao dịch ngân hàng");
        }
        await Match(); await Match();
        await using var verify = fixture.CreateDb();
        Assert.Equal("Paid", (await verify.Orders.FindAsync(seed.OrderId))!.PaymentStatus);
        Assert.Equal(1_000_000m, (await verify.Payments.FindAsync(paymentId))!.ReceivedAmount);
        Assert.Single(await verify.Set<BankTransaction>().Where(x => x.OrderId == seed.OrderId).ToListAsync());
    }

    [Fact]
    public async Task CustomerCannotReadStaffReconciliation()
    {
        var seed = await fixture.SeedOrderAsync(); await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db); var clock = new TestClock();
        await Assert.ThrowsAsync<ForbiddenException>(() => new StaffReconciliationService(uow, new TestUser(seed.CustomerId), clock, new TestProtector(), WebhookInboxTests.CreateWebhookService(uow, clock)).ListAsync());
    }

    [Fact]
    public async Task StaffCannotAcceptMoneyAlreadyReservedByLegacyRefund()
    {
        var seed = await fixture.SeedOrderAsync(); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        await using (var db = fixture.CreateDb()) { (await db.Orders.FindAsync(seed.OrderId))!.PaymentStatus = "NeedsReconciliation"; await db.SaveChangesAsync(); }
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 1_000_000));
        await using (var db = fixture.CreateDb()) { db.Add(new Payment { PaymentId = Guid.NewGuid(), OrderId = seed.OrderId, PaymentType = "Refund", Amount = 100_000, Status = "Pending", PaymentMethod = "BankTransfer", Purpose = "LegacyRefund" }); await db.SaveChangesAsync(); }
        await using var verify = fixture.CreateDb(); using var uow = new UnitOfWork(verify); var clock = new TestClock();
        await Assert.ThrowsAsync<ConflictException>(() => new StaffReconciliationService(uow, new TestUser(seed.StaffId, "Staff"), clock, new TestProtector(), WebhookInboxTests.CreateWebhookService(uow, clock)).AcceptAsync(seed.OrderId));
    }

    [Fact]
    public async Task StaffCannotAcceptExpiredOrder()
    {
        var seed = await fixture.SeedOrderAsync(); await using var db = fixture.CreateDb();
        (await db.Orders.FindAsync(seed.OrderId))!.Status = "Expired"; await db.SaveChangesAsync(); using var uow = new UnitOfWork(db); var clock = new TestClock();
        await Assert.ThrowsAsync<ConflictException>(() => new StaffReconciliationService(uow, new TestUser(seed.StaffId, "Staff"), clock, new TestProtector(), WebhookInboxTests.CreateWebhookService(uow, clock)).AcceptAsync(seed.OrderId));
    }
}
