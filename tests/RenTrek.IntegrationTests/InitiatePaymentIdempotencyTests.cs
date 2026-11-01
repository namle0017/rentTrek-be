using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.DTOs.Payment.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class InitiatePaymentIdempotencyTests(PostgresFixture fixture)
{
    internal static SePaySettings Settings => new() { BankName = "MBBank", AccountNumber = "0123456789", AccountName = "RENTREK", Api = new() { Key = "test-only-key" } };
    private async Task<PaymentResponse> Create(Seed seed, string key)
    {
        await using var db = fixture.CreateDb();
        using var uow = new UnitOfWork(db);
        var service = new PaymentService(uow, new TestClock(), Options.Create(Settings), NullLogger<PaymentService>.Instance);
        return (await service.CreateAsync(seed.CustomerId, seed.OrderId, new CreatePaymentRequest { PaymentMethod = "BankTransfer", IdempotencyKey = key })).Payment;
    }

    [Fact]
    public async Task SameKey_ReplaysOriginalPaymentAfterOrderPaid()
    {
        var seed = await fixture.SeedOrderAsync();
        var first = await Create(seed, "key-one");
        await using (var db = fixture.CreateDb())
        {
            var order = await db.Orders.FindAsync(seed.OrderId); order!.Status = "Confirmed"; order.PaymentStatus = "Paid";
            var payment = await db.Payments.FindAsync(first.PaymentId); payment!.Status = "Succeeded";
            await db.SaveChangesAsync();
        }
        Assert.Equal(first.PaymentId, (await Create(seed, "key-one")).PaymentId);
    }

    [Fact]
    public async Task SameKey_DifferentOrder_ReturnsConflict()
    {
        var a = await fixture.SeedOrderAsync(); var b = await fixture.SeedOrderAsync();
        await using (var db = fixture.CreateDb()) { (await db.Orders.FindAsync(b.OrderId))!.CustomerUserId = a.CustomerId; await db.SaveChangesAsync(); }
        await Create(a, "shared-key");
        await Assert.ThrowsAsync<ConflictException>(() => Create(b with { CustomerId = a.CustomerId }, "shared-key"));
    }

    [Fact]
    public async Task NullHold_IsNotUnlimited()
    {
        var seed = await fixture.SeedOrderAsync();
        await using (var db = fixture.CreateDb()) { (await db.Orders.FindAsync(seed.OrderId))!.HoldExpiresAt = null; await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<ConflictException>(() => Create(seed, "null-hold"));
    }

    [Fact]
    public async Task PartialPayment_RefreshesQrForRemainingAmount()
    {
        var seed = await fixture.SeedOrderAsync(400_000); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 100_000));
        var refreshed = await Create(seed, "refresh-after-partial");
        Assert.Contains("&amount=300000&", refreshed.BankTransfer.QrImageUrl);
    }

    [Theory]
    [InlineData(0)] [InlineData(-100)]
    public async Task InvalidServerTotal_DoesNotCreateTransferInstructions(int total)
    {
        var seed = await fixture.SeedOrderAsync(total);
        await Assert.ThrowsAsync<ConflictException>(() => Create(seed, "invalid-total"));
    }

    [Fact]
    public async Task ConcurrentKeys_ReuseOneActiveIntent()
    {
        var seed = await fixture.SeedOrderAsync();
        var results = await Task.WhenAll(Create(seed, "a"), Create(seed, "b"));
        Assert.Equal(results[0].PaymentId, results[1].PaymentId);
    }
}
