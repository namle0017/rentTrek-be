using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.API.BackgroundJobs;
using RenTrek.DAL.Entities;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class PaymentWorkerRecoveryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task DurableReceipt_IsRecoveredAfterRequestEnds()
    {
        var seed = await fixture.SeedOrderAsync(); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        var id = await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 1_000_000), process: false);
        await using var factory = new ApiFactory(fixture);
        var runner = new PaymentMaintenanceRunner(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<TimeProvider>(), NullLogger<PaymentMaintenanceRunner>.Instance);
        await Task.WhenAll(runner.RunOnceAsync(), runner.RunOnceAsync());
        await using var db = fixture.CreateDb();
        Assert.Equal("Processed", (await db.Set<PaymentWebhookReceipt>().FindAsync(id))!.Status);
        Assert.Equal("Paid", (await db.Orders.FindAsync(seed.OrderId))!.PaymentStatus);
        Assert.Single(await db.Set<BankTransaction>().Where(x => x.OrderId == seed.OrderId).ToListAsync());
    }

    [Fact]
    public async Task ApprovedRefund_IsNeverExecutedOrCompletedByWorker()
    {
        var workflow = new StaffRefundWorkflowTests(fixture); var (seed, id) = await workflow.Pending(); await workflow.Act(seed, id, "approve");
        await using var factory = new ApiFactory(fixture);
        var runner = new PaymentMaintenanceRunner(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<TimeProvider>(), NullLogger<PaymentMaintenanceRunner>.Instance);
        await runner.RunOnceAsync();
        await using var db = fixture.CreateDb();
        Assert.Equal("Approved", (await db.Set<RefundRequest>().FindAsync(id))!.Status);
        Assert.Empty(await db.Payments.Where(x => x.OrderId == seed.OrderId && x.PaymentType == "Refund").ToListAsync());
    }
}
