using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations.Refunds;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

public sealed class TestUser(Guid id, string role = "Customer") : ICurrentUserService
{
    public Guid? UserId => id;
    public Guid RequiredUserId => id;
    public string? Jti => "test";
    public string? Role => role;
    public bool IsAuthenticated => true;
}

[Collection("postgres")]
public sealed class CustomerRefundRequestTests(PostgresFixture fixture)
{
    internal static CreateRefundRequest Request(string account = "0012345678") => new() { BankCode = "MB", AccountNumber = account, AccountHolderName = "NGUYEN VAN A", ReasonCode = "ServiceIssue", Note = "Please review" };
    internal async Task<Seed> PaidOrder(bool trip = false)
    {
        var seed = trip ? await fixture.SeedTripOrderAsync() : await fixture.SeedOrderAsync(); var hooks = new WebhookInboxTests(fixture);
        var code = await hooks.Intent(seed); await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 1_000_000));
        return seed;
    }

    [Fact]
    public async Task Preview_UsesOrderPolicyAndVerifiedMoney()
    {
        var seed = await PaidOrder(); await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        var result = await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).PreviewAsync(seed.OrderId, "ServiceIssue");
        Assert.True(result.Eligible); Assert.Equal(500_000m, result.Amount); Assert.Equal(50m, result.RefundPercentage);
    }

    [Fact]
    public async Task Request_IsPolicyBoundProtectedAndIdempotent()
    {
        var seed = await PaidOrder();
        Guid id;
        await using (var db = fixture.CreateDb()) { using var uow = new UnitOfWork(db); id = (await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).SubmitAsync(seed.OrderId, Request(), "refund-key")).Id; }
        await using (var db = fixture.CreateDb())
        {
            using var uow = new UnitOfWork(db);
            var result = await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).SubmitAsync(seed.OrderId, Request(), "refund-key");
            Assert.Equal(id, result.Id); Assert.Equal(500_000m, result.Amount); Assert.Equal("PendingReview", result.Status);
            Assert.Equal("0012345678", result.BankDetails!.AccountNumber);
            Assert.DoesNotContain("0012345678", (await db.Set<RefundRequest>().FindAsync(id))!.ProtectedBankDetails);
            Assert.Single(await db.Set<RefundAllocation>().Where(x => x.RefundRequestId == id).ToListAsync());
        }
    }

    [Fact]
    public async Task ActiveRequest_ReservesEntirePolicyEntitlement()
    {
        var seed = await PaidOrder();
        async Task Submit(string key) { await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db); await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).SubmitAsync(seed.OrderId, Request(), key); }
        await Submit("first"); await Assert.ThrowsAsync<ConflictException>(() => Submit("second"));
    }

    [Fact]
    public async Task OneManualRefund_CanAllocateAcrossMultipleBankTransfers()
    {
        var seed = await fixture.SeedOrderAsync(400_000); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 100_000));
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 300_000));
        await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        (await db.RefundPolicyRules.FindAsync(seed.RuleId))!.RefundPercentage = 100; await db.SaveChangesAsync();
        var response = await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).SubmitAsync(seed.OrderId, Request(), "multiple-sources");
        Assert.Equal(400_000m, response.Amount);
        var allocations = await db.Set<RefundAllocation>().Where(x => x.RefundRequestId == response.Id).ToListAsync();
        Assert.Equal(new[] { 100_000m, 300_000m }, allocations.Select(x => x.Amount).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task DifferentOwner_CannotPreviewRefund()
    {
        var seed = await PaidOrder(); await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        await Assert.ThrowsAsync<NotFoundException>(() => new RefundRequestService(uow, new TestUser(Guid.NewGuid()), new TestClock(), new TestProtector()).PreviewAsync(seed.OrderId, "ServiceIssue"));
    }
}
