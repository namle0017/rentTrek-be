using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations.Refunds;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class RefundPolicyEntitlementTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(24, 500_000)] [InlineData(23, 0)] [InlineData(-1, 0)]
    public async Task PolicyHourBoundary_DeterminesRefund(int hours, int expected)
    {
        var seed = await new CustomerRefundRequestTests(fixture).PaidOrder(); await using var db = fixture.CreateDb();
        (await db.RentalOrders.FindAsync(seed.OrderId))!.RentalStartAt = TestClock.Now.UtcDateTime.AddHours(hours); await db.SaveChangesAsync(); using var uow = new UnitOfWork(db);
        var result = await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).PreviewAsync(seed.OrderId, "ServiceIssue");
        Assert.Equal(expected, result.Amount); Assert.Equal(expected > 0, result.Eligible);
    }

    [Fact]
    public async Task ConcurrentSubmissions_ReserveOnlyOneEntitlement()
    {
        var seed = await new CustomerRefundRequestTests(fixture).PaidOrder();
        async Task<bool> Submit(string key)
        {
            await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
            try { await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).SubmitAsync(seed.OrderId, CustomerRefundRequestTests.Request(), key); return true; }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Submit("a"), Submit("b")); Assert.Single(results, x => x);
        await using var verify = fixture.CreateDb();
        Assert.Equal(500_000m, await verify.Set<RefundRequest>().Where(x => x.OrderId == seed.OrderId).SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task ApprovalKeepsSubmittedPolicyVersion_WhenPolicyChanges()
    {
        var workflow = new StaffRefundWorkflowTests(fixture); var (seed, id) = await workflow.Pending();
        await using (var db = fixture.CreateDb()) { (await db.RefundPolicies.FindAsync(seed.PolicyId))!.Version = 2; (await db.RefundPolicyRules.FindAsync(seed.RuleId))!.RefundPercentage = 100; await db.SaveChangesAsync(); }
        await workflow.Act(seed, id, "approve");
        await using var verify = fixture.CreateDb(); using var uow = new UnitOfWork(verify);
        var response = await new StaffRefundRequestService(uow, new TestUser(seed.StaffId, "Staff"), new TestClock(), new TestProtector()).GetAsync(id);
        Assert.Equal(1, response.PolicyVersion); Assert.Equal(50, response.RefundPercentage); Assert.Equal(500_000m, response.Amount);
    }

    [Fact]
    public async Task LaterCollectedMoney_IncreasesPolicyCapWithoutApplyingPercentageToRemainingBalance()
    {
        var seed = await fixture.SeedOrderAsync(400_000); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 100_000));
        async Task<RefundRequestResponse> Submit(string key)
        {
            await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
            return await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).SubmitAsync(seed.OrderId, CustomerRefundRequestTests.Request(), key);
        }
        var first = await Submit("partial-source"); Assert.Equal(50_000m, first.Amount);
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 300_000));
        var second = await Submit("later-source"); Assert.Equal(150_000m, second.Amount);
        var workflow = new StaffRefundWorkflowTests(fixture);
        await workflow.Act(seed, first.Id, "approve"); await workflow.Act(seed, second.Id, "approve");
        await workflow.Act(seed, first.Id, "done"); await workflow.Act(seed, second.Id, "done");
        await using var verify = fixture.CreateDb();
        Assert.Equal(200_000m, await verify.Payments.Where(x => x.OrderId == seed.OrderId && x.PaymentType == "Refund" && x.Status == "Succeeded").SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task TripManualRefund_UsesTripStartAndOneCompletion()
    {
        var workflow = new StaffRefundWorkflowTests(fixture); var (seed, id) = await workflow.Pending(trip: true);
        await workflow.Act(seed, id, "approve"); await workflow.Act(seed, id, "done");
        await using var verify = fixture.CreateDb();
        Assert.Equal("Done", (await verify.Set<RefundRequest>().FindAsync(id))!.Status);
        Assert.Equal(500_000m, (await verify.Payments.SingleAsync(x => x.OrderId == seed.OrderId && x.PaymentType == "Refund")).Amount);
    }
}
