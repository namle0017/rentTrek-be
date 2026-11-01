using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

public sealed class TestProtector : IPaymentDataProtector
{
    public string Protect(string value) => "test:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    public string Unprotect(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value[5..]));
}

[Collection("postgres")]
public sealed class WebhookInboxTests(PostgresFixture fixture)
{
    internal static long NextEventId() => Interlocked.Increment(ref eventId);
    private static long eventId = 1000;
    internal static string Payload(long id, string code, decimal amount, string account = "0123456789", string direction = "in")
        => JsonSerializer.Serialize(new SePayWebhookRequest { Id = id, Gateway = "MBBank", AccountNumber = account, TransferType = direction, TransferAmount = amount, Content = code, ReferenceCode = $"bank-{id}" });
    internal static PaymentService CreatePaymentService(UnitOfWork uow, TestClock clock) => new(uow, clock, Options.Create(InitiatePaymentIdempotencyTests.Settings), NullLogger<PaymentService>.Instance);
    internal static SePayWebhookService CreateWebhookService(UnitOfWork uow, TestClock clock) => new(uow, clock, Options.Create(InitiatePaymentIdempotencyTests.Settings), new TestProtector());
    internal async Task<string> Intent(Seed seed)
    {
        await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        return (await CreatePaymentService(uow, new TestClock()).CreateAsync(seed.CustomerId, seed.OrderId, new() { PaymentMethod = "BankTransfer", IdempotencyKey = "intent" })).Payment.BankTransfer.TransferContent;
    }
    internal async Task<Guid> Send(string body, TestClock? clock = null, bool process = true)
    {
        Guid id;
        await using (var db = fixture.CreateDb()) { using var uow = new UnitOfWork(db); id = await CreateWebhookService(uow, clock ?? new()).ReceiveAsync(body, "Apikey test-only-key"); }
        if (process) { await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db); await CreateWebhookService(uow, clock ?? new()).ProcessAsync(id); }
        return id;
    }

    [Fact]
    public async Task UnmatchedMoney_IsDurableBeforeAcknowledgement()
    {
        var id = await Send(Payload(NextEventId(), "NO-CODE", 100));
        await using var db = fixture.CreateDb();
        var receipt = await db.Set<PaymentWebhookReceipt>().FindAsync(id);
        Assert.Equal("Unmatched", receipt!.Status);
        Assert.DoesNotContain("NO-CODE", receipt.ProtectedPayload);
        Assert.Single(await db.Set<BankTransaction>().Where(x => x.ReceiptId == id).ToListAsync());
    }

    [Fact]
    public async Task WrongReceivingAccount_NeverConfirmsOrder()
    {
        var seed = await fixture.SeedOrderAsync(); var code = await Intent(seed);
        var id = await Send(Payload(NextEventId(), code, 1_000_000, "WRONG"));
        await using var db = fixture.CreateDb();
        Assert.Equal("Unpaid", (await db.Orders.FindAsync(seed.OrderId))!.PaymentStatus);
        Assert.Equal("Rejected", (await db.Set<PaymentWebhookReceipt>().FindAsync(id))!.Status);
    }

    [Fact]
    public async Task PartialTransfers_AccumulateRegardlessOfArrivalOrder()
    {
        var seed = await fixture.SeedOrderAsync(400_000); var code = await Intent(seed);
        await Send(Payload(NextEventId(), code, 100_000));
        await Send(Payload(NextEventId(), code, 300_000));
        await using var db = fixture.CreateDb();
        Assert.Equal("Confirmed", (await db.Orders.FindAsync(seed.OrderId))!.Status);
        Assert.Equal(400_000m, (await db.Payments.SingleAsync(x => x.OrderId == seed.OrderId)).ReceivedAmount);
        Assert.Equal(2, await db.Set<BankTransaction>().CountAsync(x => x.OrderId == seed.OrderId));
    }

    [Fact]
    public async Task DuplicateWebhook_OnlyAddsMoneyOnce()
    {
        var seed = await fixture.SeedOrderAsync(); var code = await Intent(seed);
        var body = Payload(NextEventId(), code, 1_000_000);
        var ids = await Task.WhenAll(Send(body), Send(body));
        Assert.Equal(ids[0], ids[1]);
        await using var db = fixture.CreateDb();
        Assert.Single(await db.Set<BankTransaction>().Where(x => x.OrderId == seed.OrderId).ToListAsync());
    }

    [Fact]
    public async Task MoneyAtDeadline_IsRecordedForReview()
    {
        var seed = await fixture.SeedOrderAsync(); var code = await Intent(seed);
        await Send(Payload(NextEventId(), code, 1_000_000), new TestClock { Current = TestClock.Now.AddMinutes(15) });
        await using var db = fixture.CreateDb();
        Assert.NotEqual("Confirmed", (await db.Orders.FindAsync(seed.OrderId))!.Status);
        Assert.Equal("NeedsReconciliation", (await db.Set<BankTransaction>().SingleAsync(x => x.OrderId == seed.OrderId)).Status);
    }
}
