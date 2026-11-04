using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOps.Requests;
using RenTrek.BLL.DTOs.RentalOps.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;

namespace RenTrek.BLL.Tests;

/// <summary>Luồng tiền cọc / phí của đơn thuê chạy trên PostgreSQL thật (advisory lock, transaction).</summary>
public sealed class RentalFulfillmentServicePostgresTests
{
    [AdminPostgresFact]
    public async Task FullFlow_LateReturn_SuggestsDraftFee_AndSettlesAgainstDeposit()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        var clock = new MutableClock(database.Now);
        var orderId = await PrepareAsync(database, start: database.Now.AddHours(1), end: database.Now.AddDays(1), deposit: 500000);

        var claimed = await RunAsync(database, clock, s => s.ClaimAsync(orderId), database.StaffId, RoleNames.Staff);
        Assert.Equal("Staff Test", claimed.HandledByName);
        await Assert.ThrowsAsync<ConflictException>(() => RunAsync(database, clock, s => s.ClaimAsync(orderId), database.OtherStaffId, RoleNames.Staff));

        var issued = await RunAsync(database, clock, s => s.HandoverAsync(orderId, new HandoverRentalRequest()), database.StaffId, RoleNames.Staff);
        Assert.Equal(OrderStatuses.InFulfillment, issued.Status);
        var assignmentId = Assert.Single(issued.Assignments, a => a.Status == AssignmentStatuses.Issued).AssignmentId;

        // Trả trễ 27 giờ, ân hạn 2 giờ → 2 ngày × 200.000
        clock.Now = database.Now.AddDays(2).AddHours(3);
        var returned = await RunAsync(database, clock, s => s.ReturnAsync(orderId, Return(assignmentId, ReturnConditions.Normal)));
        var fee = Assert.Single(returned.Charges);
        Assert.Equal((RentalChargeTypes.LateReturn, RentalChargeStatuses.Draft, 400000m), (fee.ChargeType, fee.Status, fee.Amount));
        Assert.Equal(EquipmentAssetStatuses.Available, Assert.Single(returned.Assignments).AssetStatus);
        Assert.False(returned.AllowedActions.CanComplete);

        await Assert.ThrowsAsync<ConflictException>(() => RunAsync(database, clock, s => s.ReturnAsync(orderId, Return(assignmentId, ReturnConditions.Normal))));
        await Assert.ThrowsAsync<ConflictException>(() => RunAsync(database, clock, s => s.CompleteAsync(orderId)));

        await RunAsync(database, clock, s => s.AssessChargeAsync(orderId, fee.ChargeId));
        var completed = await RunAsync(database, clock, s => s.CompleteAsync(orderId));

        Assert.Equal(OrderStatuses.Completed, completed.Status);
        var settlement = completed.Settlement;
        Assert.Equal((500000m, 400000m, 100000m, 0m), (settlement.DepositHeld, settlement.ValidCharges, settlement.DepositToRefund, settlement.AdditionalAmountDue));
        await using var context = database.Context();
        Assert.Equal(["RentalChargeAssess", "RentalClaim", "RentalComplete", "RentalHandover", "RentalReturn"],
            await context.AuditLogs.OrderBy(x => x.Action).Select(x => x.Action).ToListAsync());
    }

    [AdminPostgresFact]
    public async Task Handover_IsBlocked_WhenRentalEnded_TooEarly_OrUnpaid()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        var clock = new MutableClock(database.Now);
        var handover = (Guid id) => RunAsync(database, clock, s => s.HandoverAsync(id, new HandoverRentalRequest()));

        var orderId = await PrepareAsync(database, start: database.Now.AddDays(-2), end: database.Now.AddDays(-1), deposit: 0);
        await Assert.ThrowsAsync<ConflictException>(() => handover(orderId));

        await PrepareAsync(database, start: database.Now.AddDays(2), end: database.Now.AddDays(3), deposit: 0);
        await Assert.ThrowsAsync<ConflictException>(() => handover(orderId));

        await PrepareAsync(database, start: database.Now.AddHours(1), end: database.Now.AddDays(1), deposit: 0);
        await using (var context = database.Context())
            await context.Orders.Where(x => x.OrderId == orderId).ExecuteUpdateAsync(x => x.SetProperty(o => o.PaymentStatus, OrderPaymentStatuses.Unpaid));
        await Assert.ThrowsAsync<ConflictException>(() => handover(orderId));

        await using var check = database.Context();
        Assert.Equal(AssignmentStatuses.Reserved, (await check.AssetAssignments.SingleAsync()).Status);
        Assert.Empty(await check.AuditLogs.ToListAsync());
    }

    [AdminPostgresFact]
    public async Task LostAsset_LeavesStock_AndOnlyAdminVoidsAssessedCharge()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        var clock = new MutableClock(database.Now);
        var orderId = await PrepareAsync(database, start: database.Now.AddHours(-1), end: database.Now.AddDays(1), deposit: 100000);

        var issued = await RunAsync(database, clock, s => s.ScanHandoverAsync(orderId, new ScanHandoverRequest { AssetCode = " tent-001 " }));
        var assignmentId = Assert.Single(issued.Assignments).AssignmentId;

        var returned = await RunAsync(database, clock, s => s.ReturnAsync(orderId, Return(assignmentId, ReturnConditions.Lost)));
        var lost = Assert.Single(returned.Assignments);
        Assert.Equal((AssignmentStatuses.LostOrException, EquipmentAssetStatuses.Lost), (lost.Status, lost.AssetStatus));
        Assert.Empty(returned.Charges);

        await Assert.ThrowsAsync<NotFoundException>(() => RunAsync(database, clock, s => s.CreateChargeAsync(orderId, LossCharge(Guid.NewGuid()))));
        var charged = await RunAsync(database, clock, s => s.CreateChargeAsync(orderId, LossCharge(assignmentId)));
        var charge = Assert.Single(charged.Charges);
        Assert.Equal((RentalChargeStatuses.Assessed, "TENT-001"), (charge.Status, charge.AssetCode));
        Assert.Equal((0m, 200000m), (charged.Settlement.DepositToRefund, charged.Settlement.AdditionalAmountDue));

        var reason = new VoidRentalChargeRequest { Reason = "Khách tìm thấy" };
        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(database, clock, s => s.VoidChargeAsync(orderId, charge.ChargeId, reason), database.StaffId, RoleNames.Staff));
        var voided = await RunAsync(database, clock, s => s.VoidChargeAsync(orderId, charge.ChargeId, reason));
        Assert.Equal(RentalChargeStatuses.Voided, Assert.Single(voided.Charges).Status);
        await Assert.ThrowsAsync<ConflictException>(() => RunAsync(database, clock, s => s.VoidChargeAsync(orderId, charge.ChargeId, reason)));

        var completed = await RunAsync(database, clock, s => s.CompleteAsync(orderId));
        Assert.Equal((0m, 100000m), (completed.Settlement.ValidCharges, completed.Settlement.DepositToRefund));
    }

    private static async Task<Guid> PrepareAsync(AdminTestDatabase database, DateTimeOffset start, DateTimeOffset end, decimal deposit)
    {
        await using var context = database.Context();
        var orderId = await context.Orders.Where(x => x.OrderCode == "RENT-TEST").Select(x => x.OrderId).SingleAsync();
        await context.RentalOrders.Where(x => x.OrderId == orderId).ExecuteUpdateAsync(x => x
            .SetProperty(r => r.RentalStartAt, start.UtcDateTime)
            .SetProperty(r => r.RentalEndAt, end.UtcDateTime)
            .SetProperty(r => r.DepositAmount, deposit));
        return orderId;
    }

    // Mỗi bước một DbContext mới, giống mỗi request HTTP.
    private static async Task<AdminRentalOrderDetailResponse> RunAsync(AdminTestDatabase database, TimeProvider clock,
        Func<RentalFulfillmentService, Task<AdminRentalOrderDetailResponse>> action, Guid? actorId = null, string role = RoleNames.Admin)
    {
        await using var context = database.Context();
        return await action(database.Fulfillment(context, clock, actorId, role));
    }

    private static ReturnRentalAssetsRequest Return(Guid assignmentId, string condition)
        => new() { Items = [new ReturnRentalAssetItemRequest { AssignmentId = assignmentId, Condition = condition }] };

    private static CreateRentalChargeRequest LossCharge(Guid assignmentId)
        => new() { ChargeType = RentalChargeTypes.Loss, Amount = 300000, Reason = "Mất lều", AssignmentId = assignmentId };

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
