using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Entities;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class PaymentLedgerDatabaseTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ActiveCollectionIntent_IsUniquePerOrder()
    {
        var seed = await fixture.SeedOrderAsync(); await using var db = fixture.CreateDb();
        db.Add(new Payment { PaymentId = Guid.NewGuid(), OrderId = seed.OrderId, PaymentType = "Collection", Amount = 100, Status = "Pending", PaymentMethod = "BankTransfer", Purpose = "RentalFee" });
        await db.SaveChangesAsync();
        db.Add(new Payment { PaymentId = Guid.NewGuid(), OrderId = seed.OrderId, PaymentType = "Collection", Amount = 100, Status = "Pending", PaymentMethod = "BankTransfer", Purpose = "RentalFee" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task ProviderTransactionUniqueConstraint_PreventsCrossOrderDuplicate()
    {
        await using var db = fixture.CreateDb();
        var receipt = new PaymentWebhookReceipt { ProviderEventId = Guid.NewGuid().ToString(), ReceivedAt = TestClock.Now };
        db.Add(receipt);
        db.Add(new BankTransaction { ReceiptId = receipt.PaymentWebhookReceiptId, ProviderTransactionId = receipt.ProviderEventId, Amount = 100, ReceivedAt = TestClock.Now });
        await db.SaveChangesAsync();
        db.Add(new BankTransaction { ReceiptId = receipt.PaymentWebhookReceiptId, ProviderTransactionId = receipt.ProviderEventId, Amount = 200, ReceivedAt = TestClock.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task BankTransaction_RejectsNegativeAmount()
    {
        await using var db = fixture.CreateDb();
        var receipt = new PaymentWebhookReceipt { ProviderEventId = Guid.NewGuid().ToString(), ReceivedAt = TestClock.Now };
        db.Add(receipt);
        db.Add(new BankTransaction { ReceiptId = receipt.PaymentWebhookReceiptId, ProviderTransactionId = receipt.ProviderEventId, Amount = -1, ReceivedAt = TestClock.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
