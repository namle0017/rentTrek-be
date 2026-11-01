using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations.Refunds;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class StaffRefundWorkflowTests(PostgresFixture fixture)
{
    internal async Task<(Seed Seed, Guid RequestId)> Pending(bool trip = false)
    {
        var seed = await new CustomerRefundRequestTests(fixture).PaidOrder(trip);
        await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        var r = await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector())
            .SubmitAsync(seed.OrderId, CustomerRefundRequestTests.Request(), "customer-request");
        return (seed, r.Id);
    }
    internal async Task Act(Seed seed, Guid id, string action, string key = "done-key", Guid? staffId = null, string? transferReference = null)
    {
        await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        var service = new StaffRefundRequestService(uow, new TestUser(staffId ?? seed.StaffId, "Staff"), new TestClock(), new TestProtector());
        if (action == "approve") await service.ApproveAsync(id);
        else if (action == "reject") await service.RejectAsync(id, "Không đủ chứng từ");
        else await service.CompleteAsync(id, new CompleteRefundRequest { TransferReference = transferReference ?? $"BANK-{id:N}", TransferredAt = TestClock.Now }, key);
    }

    [Fact]
    public async Task Approval_DoesNotMeanMoneyTransferred()
    {
        var (seed, id) = await Pending(); await Act(seed, id, "approve");
        await using var db = fixture.CreateDb();
        Assert.Equal("Approved", (await db.Set<RefundRequest>().FindAsync(id))!.Status);
        Assert.Empty(await db.Payments.Where(x => x.OrderId == seed.OrderId && x.PaymentType == "Refund").ToListAsync());
        Assert.Equal("Paid", (await db.Orders.FindAsync(seed.OrderId))!.PaymentStatus);
    }

    [Fact]
    public async Task Done_IsIdempotentAndPolicyCannotBeAppliedAgain()
    {
        var (seed, id) = await Pending(); await Act(seed, id, "approve");
        await Task.WhenAll(Act(seed, id, "done"), Act(seed, id, "done"));
        await using var db = fixture.CreateDb();
        var refund = Assert.Single(await db.Payments.Where(x => x.OrderId == seed.OrderId && x.PaymentType == "Refund").ToListAsync());
        Assert.Equal("Succeeded", refund.Status); Assert.Equal(500_000m, refund.Amount);
        Assert.Equal("Done", (await db.Set<RefundRequest>().FindAsync(id))!.Status);
        Assert.Equal("RefundRequest", (await db.AuditLogs.SingleAsync(x => x.EntityId == id && x.Action == "Refund.Completed")).EntityType);
        Assert.Equal("PartiallyRefunded", (await db.Orders.FindAsync(seed.OrderId))!.PaymentStatus);
        using var uow = new UnitOfWork(db);
        var preview = await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).PreviewAsync(seed.OrderId, "ServiceIssue");
        Assert.False(preview.Eligible); Assert.Equal(0, preview.Amount);
    }

    [Fact]
    public async Task PendingReview_CannotBeCompleted()
    {
        var (seed, id) = await Pending(); await Assert.ThrowsAsync<ConflictException>(() => Act(seed, id, "done"));
    }

    [Fact]
    public async Task Reject_ReleasesReservationAndPreservesHistory()
    {
        var (seed, id) = await Pending(); await Act(seed, id, "reject");
        await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        var result = await new RefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).SubmitAsync(seed.OrderId, CustomerRefundRequestTests.Request(), "resubmit");
        Assert.Equal(500_000m, result.Amount); Assert.NotEqual(id, result.Id);
        Assert.Equal("Rejected", (await db.Set<RefundRequest>().FindAsync(id))!.Status);
    }

    [Fact]
    public async Task OnlyAssignedStaff_CanCompleteApprovedRequest()
    {
        var (seed, id) = await Pending(); await Act(seed, id, "approve");
        var other = await fixture.SeedOrderAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => Act(seed, id, "done", staffId: other.StaffId));
    }

    [Fact]
    public async Task SameBankTransfer_CannotCompleteTwoDifferentRequests()
    {
        var (a, first) = await Pending(); var (b, second) = await Pending();
        await Act(a, first, "approve"); await Act(b, second, "approve");
        var reference = $"SHARED-{Guid.NewGuid():N}";
        await Act(a, first, "done", transferReference: reference);
        await Assert.ThrowsAsync<ConflictException>(() => Act(b, second, "done", transferReference: reference));
        await using var db = fixture.CreateDb();
        Assert.Equal("Approved", (await db.Set<RefundRequest>().FindAsync(second))!.Status);
        Assert.Empty(await db.Payments.Where(x => x.OrderId == b.OrderId && x.PaymentType == "Refund").ToListAsync());
    }

    [Fact]
    public async Task Customer_CannotApproveTheirOwnRequest()
    {
        var (seed, id) = await Pending(); await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        await Assert.ThrowsAsync<ForbiddenException>(() => new StaffRefundRequestService(uow, new TestUser(seed.CustomerId), new TestClock(), new TestProtector()).ApproveAsync(id));
    }
}
