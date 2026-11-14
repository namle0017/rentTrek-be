using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Validators.RentalOrder;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class RentalExtensionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid VariantId = Guid.NewGuid();
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly Guid AssignmentId = Guid.NewGuid();

    // Đơn 3 ngày (bậc 1–3 ngày, 100.000/ngày); gia hạn thêm 2 ngày thì nhảy sang bậc từ 4 ngày (80.000/ngày)
    private static readonly DateTime StartUtc = Now.UtcDateTime.AddDays(-1);
    private static readonly DateTime EndUtc = Now.UtcDateTime.AddDays(2);
    private static readonly DateTime NewEndUtc = Now.UtcDateTime.AddDays(4);

    [Fact]
    public async Task QuoteExtension_WhenNewDaysJumpToAnotherTier_ComputesExtraFromNewTierWithoutWriting()
    {
        var harness = new Harness();

        var quote = await harness.Service.QuoteExtensionAsync(CustomerId, OrderId, new ExtendRentalQuoteRequest { NewEndAt = NewEndUtc });

        Assert.Equal(OrderId, quote.OrderId);
        Assert.Equal(EndUtc, quote.OldEndAt);
        Assert.Equal(NewEndUtc, quote.NewEndAt);
        Assert.Equal((3, 5), (quote.OldBillableDays, quote.NewBillableDays));
        Assert.Equal(100_000m, quote.ExtraAmount);
        var line = Assert.Single(quote.Lines);
        Assert.Equal(ItemId, line.RentalOrderItemId);
        Assert.Equal("Lều — 2 người", line.DisplayName);
        Assert.Equal((3, 5), (line.OldBillableDays, line.NewBillableDays));
        Assert.Equal((100_000m, 80_000m), (line.OldDailyPrice, line.NewDailyPrice));
        Assert.Equal((300_000m, 400_000m), (line.OldLineAmount, line.NewLineAmount));
        Assert.Equal(0, harness.BeginCount);
        Assert.Equal(0, harness.SaveCount);
    }

    [Fact]
    public async Task QuoteExtension_WhenRepricedTotalIsLower_ExtraAmountIsZero()
    {
        var harness = new Harness(tiers: [Tier(1, 3, 100_000m), Tier(4, null, 50_000m)]);

        var quote = await harness.Service.QuoteExtensionAsync(CustomerId, OrderId, new ExtendRentalQuoteRequest { NewEndAt = NewEndUtc });

        Assert.Equal(0m, quote.ExtraAmount);
        Assert.Equal(250_000m, Assert.Single(quote.Lines).NewLineAmount);
    }

    [Fact]
    public async Task QuoteExtension_WhenOrderNotOwned_Throws404()
    {
        var harness = new Harness();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => harness.Service.QuoteExtensionAsync(Guid.NewGuid(), OrderId, new ExtendRentalQuoteRequest { NewEndAt = NewEndUtc }));

        Assert.Equal(ErrorCodes.OrderNotFound, exception.ErrorCode);
    }

    [Theory]
    [InlineData(OrderStatuses.Completed, 2)]
    [InlineData(OrderStatuses.PendingPayment, 2)]
    [InlineData(OrderStatuses.Cancelled, 2)]
    [InlineData(OrderStatuses.InFulfillment, -4)]
    public async Task QuoteExtension_WhenOrderNotActiveOrAlreadyEnded_Throws409NotExtendable(string status, int endOffsetDays)
    {
        var detail = Detail(status, Now.UtcDateTime.AddDays(-3), Now.UtcDateTime.AddDays(endOffsetDays));
        var harness = new Harness(detail: detail);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => harness.Service.QuoteExtensionAsync(CustomerId, OrderId, new ExtendRentalQuoteRequest { NewEndAt = Now.UtcDateTime.AddDays(5) }));

        Assert.Equal(ErrorCodes.RentalNotExtendable, exception.ErrorCode);
    }

    [Fact]
    public async Task QuoteExtension_WhenNewEndEqualsCurrentEnd_Throws409NotExtendable()
    {
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => new Harness().Service.QuoteExtensionAsync(CustomerId, OrderId, new ExtendRentalQuoteRequest { NewEndAt = EndUtc }));

        Assert.Equal(ErrorCodes.RentalNotExtendable, exception.ErrorCode);
    }

    [Fact]
    public async Task QuoteExtension_WhenShorteningOrExceedingMaxDays_Throws400InvalidPeriod()
    {
        var harness = new Harness();

        var shorten = await Assert.ThrowsAsync<BadRequestException>(
            () => harness.Service.QuoteExtensionAsync(CustomerId, OrderId, new ExtendRentalQuoteRequest { NewEndAt = EndUtc.AddHours(-1) }));
        var tooLong = await Assert.ThrowsAsync<BadRequestException>(
            () => harness.Service.QuoteExtensionAsync(CustomerId, OrderId, new ExtendRentalQuoteRequest { NewEndAt = StartUtc.AddDays(61) }));

        Assert.Equal(ErrorCodes.InvalidRentalPeriod, shorten.ErrorCode);
        Assert.Equal(ErrorCodes.InvalidRentalPeriod, tooLong.ErrorCode);
    }

    [Fact]
    public async Task QuoteExtension_WhenAssetBlockedByAnotherAssignment_Throws409WithAssetCodesAndChecksOnlyExtraWindow()
    {
        var harness = new Harness(blockedAssetIds: [AssetId]);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => harness.Service.QuoteExtensionAsync(CustomerId, OrderId, new ExtendRentalQuoteRequest { NewEndAt = NewEndUtc }));

        Assert.Equal(ErrorCodes.ResourceUnavailable, exception.ErrorCode);
        Assert.Contains("TENT-001", exception.Errors!["assetCodes"]);
        var filter = harness.LastFilter!;
        Assert.Equal((EndUtc, NewEndUtc), (filter.WindowStart, filter.WindowEnd));
        Assert.Equal(new[] { AssignmentStatuses.Reserved, AssignmentStatuses.Issued }, filter.BlockingAssignmentStatuses.ToArray());
        Assert.Equal(AssignmentStatuses.Issued, filter.IssuedStatus);
        Assert.Equal(Now.UtcDateTime, filter.NowUtc);
        Assert.Equal(new[] { AssignmentId }, harness.LastExcludedAssignmentIds);
        Assert.Equal(new[] { AssetId }, harness.LastCheckedAssetIds);
    }

    [Fact]
    public async Task Extend_WhenAcceptedAmountDiffers_Throws409PriceChangedAndRollsBackWithoutChanges()
    {
        var harness = new Harness();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => harness.Service.ExtendAsync(
            CustomerId, OrderId, new ExtendRentalOrderRequest { NewEndAt = NewEndUtc, AcceptedExtraAmount = 99_000m }));

        Assert.Equal(ErrorCodes.RentalExtensionPriceChanged, exception.ErrorCode);
        Assert.Equal((1, 0, 0, 1), (harness.BeginCount, harness.SaveCount, harness.CommitCount, harness.RollbackCount));
        Assert.Equal(EndUtc, harness.Tracked.RentalOrder!.RentalEndAt);
        Assert.Empty(harness.Adjustments);
        Assert.Empty(harness.Charges);
    }

    [Fact]
    public async Task Extend_WithExtraAmount_CreatesPendingPaymentAndHoldsAssets_AppliesOnlyWhenPaid()
    {
        var harness = new Harness();

        var result = await harness.Service.ExtendAsync(
            CustomerId, OrderId, new ExtendRentalOrderRequest { NewEndAt = NewEndUtc, AcceptedExtraAmount = 100_000m });

        Assert.Equal((1, 1, 1, 0), (harness.BeginCount, harness.SaveCount, harness.CommitCount, harness.RollbackCount));
        Assert.True(harness.FulfillmentLocked);
        Assert.Equal(new[] { VariantId }, harness.LockedVariants);
        Assert.Equal(Now.UtcDateTime, harness.LastFilter!.NowUtc);

        // Chưa thanh toán: hạn trả / giá dòng giữ nguyên, thiết bị được giữ chỗ tới hạn mới
        var rental = harness.Tracked.RentalOrder!;
        Assert.Equal(EndUtc, rental.RentalEndAt);
        var item = Assert.Single(rental.RentalOrderItems);
        Assert.Equal(NewEndUtc, Assert.Single(item.AssetAssignments).ReservedUntil);
        Assert.Empty(harness.Charges);

        var payment = Assert.Single(harness.Payments);
        Assert.Equal((PaymentPurposes.RentalExtension, PaymentStatuses.Pending, 100_000m), (payment.Purpose, payment.Status, payment.Amount));
        var adjustment = Assert.Single(harness.Adjustments);
        Assert.Equal((RentalAdjustmentStatuses.Pending, payment.PaymentId, 100_000m), (adjustment.Status, adjustment.PaymentId, adjustment.ExtraAmount));
        Assert.Equal(adjustment.RentalOrderAdjustmentId, result.Adjustment.Id);
        Assert.Equal(OrderId, result.Order.Id);

        // Webhook xác nhận tiền vào: áp hạn mới, tính lại giá dòng, báo khách
        await harness.Service.ApplyPaidExtensionAsync(harness.Tracked, adjustment, Now.UtcDateTime);
        Assert.Equal(NewEndUtc, rental.RentalEndAt);
        Assert.Equal((5, 80_000m, 400_000m), (item.BillableDays, item.DailyPrice, item.LineAmount));
        Assert.Equal(RentalAdjustmentStatuses.Applied, adjustment.Status);
        var notification = Assert.Single(harness.Notifications);
        Assert.Equal((CustomerId, NotificationTypes.RentalExtended, $"RentalExtended:{adjustment.RentalOrderAdjustmentId}"),
            (notification.Recipient, notification.Type, notification.EventKey));
    }

    [Fact]
    public async Task Extend_LocksVariantOfEveryHeldAsset_IncludingBundleComponents()
    {
        var harness = new Harness();
        var componentVariantId = Guid.NewGuid();
        Assert.Single(Assert.Single(harness.Tracked.RentalOrder!.RentalOrderItems).AssetAssignments)
            .EquipmentAsset.ProductVariantId = componentVariantId;

        await harness.Service.ExtendAsync(
            CustomerId, OrderId, new ExtendRentalOrderRequest { NewEndAt = NewEndUtc, AcceptedExtraAmount = 100_000m });

        Assert.Equal(new[] { VariantId, componentVariantId }.Order(), harness.LockedVariants.Order());
    }

    [Fact]
    public async Task Extend_WhenExtraAmountIsZero_DoesNotCreateCharge()
    {
        var harness = new Harness(tiers: [Tier(1, 3, 100_000m), Tier(4, null, 50_000m)]);

        await harness.Service.ExtendAsync(CustomerId, OrderId, new ExtendRentalOrderRequest { NewEndAt = NewEndUtc, AcceptedExtraAmount = 0m });

        Assert.Equal(0m, Assert.Single(harness.Adjustments).ExtraAmount);
        Assert.Empty(harness.Charges);
    }

    [Fact]
    public async Task Extend_WhenConflictAppearsInsideLock_Throws409AndRollsBack()
    {
        var harness = new Harness(blockedAssetIds: [AssetId]);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => harness.Service.ExtendAsync(
            CustomerId, OrderId, new ExtendRentalOrderRequest { NewEndAt = NewEndUtc, AcceptedExtraAmount = 100_000m }));

        Assert.Equal(ErrorCodes.ResourceUnavailable, exception.ErrorCode);
        Assert.Equal((0, 1), (harness.SaveCount, harness.RollbackCount));
        Assert.Equal(EndUtc, Assert.Single(Assert.Single(harness.Tracked.RentalOrder!.RentalOrderItems).AssetAssignments).ReservedUntil);
    }

    [Fact]
    public async Task GetAdjustments_MapsRowsAndRejectsOtherUsersWith404()
    {
        var row = new RentalOrderAdjustmentRow
        {
            Id = Guid.NewGuid(),
            AdjustmentType = RentalAdjustmentTypes.Extension,
            OldEndAt = EndUtc,
            NewEndAt = NewEndUtc,
            ExtraAmount = 100_000m,
            RequestedByUserId = CustomerId,
            RequestedByName = "Khách Test",
            CreatedAt = Now.UtcDateTime
        };
        var harness = new Harness(adjustments: [row]);

        var items = await harness.Service.GetAdjustmentsAsync(CustomerId, OrderId);
        var item = Assert.Single(items);
        Assert.Equal((row.Id, "Khách Test", 100_000m, NewEndUtc), (item.Id, item.RequestedByName, item.ExtraAmount, item.NewEndAt));
        Assert.Equal(CustomerId, harness.LastAdjustmentsCustomerId);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAdjustmentsAsync(Guid.NewGuid(), OrderId));
        Assert.Equal(ErrorCodes.OrderNotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task AdminGetAdjustments_ReadsAnyOrderAndReturns404WhenMissing()
    {
        var harness = new Harness(adjustments: []);
        var fulfillment = new RentalFulfillmentService(harness.UnitOfWork, TestProxy.Create<ICurrentUserService>(), new FixedClock(Now));

        Assert.Empty(await fulfillment.GetAdjustmentsAsync(OrderId));
        Assert.Null(harness.LastAdjustmentsCustomerId);
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => fulfillment.GetAdjustmentsAsync(Guid.NewGuid()));
        Assert.Equal(ErrorCodes.OrderNotFound, exception.ErrorCode);
    }

    [Fact]
    public void Validators_RequireNewEndAtAndNonNegativeAcceptedAmount()
    {
        Assert.False(new ExtendRentalQuoteRequestValidator().Validate(new ExtendRentalQuoteRequest()).IsValid);
        Assert.True(new ExtendRentalQuoteRequestValidator().Validate(new ExtendRentalQuoteRequest { NewEndAt = NewEndUtc }).IsValid);

        var invalid = new ExtendRentalOrderRequestValidator().Validate(new ExtendRentalOrderRequest { AcceptedExtraAmount = -1 });
        Assert.Contains(invalid.Errors, e => e.PropertyName == nameof(ExtendRentalOrderRequest.NewEndAt));
        Assert.Contains(invalid.Errors, e => e.PropertyName == nameof(ExtendRentalOrderRequest.AcceptedExtraAmount));
        Assert.True(new ExtendRentalOrderRequestValidator()
            .Validate(new ExtendRentalOrderRequest { NewEndAt = NewEndUtc, AcceptedExtraAmount = 0 }).IsValid);
    }

    [Fact]
    public void ExtensionChargeType_IsNotAllowedForManualStaffCharges()
    {
        Assert.Equal("Extension", RentalChargeTypes.Extension);
        Assert.DoesNotContain(RentalChargeTypes.Extension, RentalChargeTypes.All);
    }

    // UAT-19: đơn A giữ TENT-001 tới ngày 10, đơn B giữ TENT-001 từ ngày 11.
    [AdminPostgresFact]
    public async Task Extension_Postgres_RejectsOverlapWithOtherOrder_AppliesAtomically_AndChecksOwnershipAndStatus()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        var day10 = database.Now.UtcDateTime.AddDays(6);
        var day11 = database.Now.UtcDateTime.AddDays(7);
        var day12 = database.Now.UtcDateTime.AddDays(8);
        Guid orderA, orderB;

        await using (var context = database.Context())
        {
            var a = await context.Orders
                .Include(o => o.RentalOrder!).ThenInclude(r => r.RentalOrderItems).ThenInclude(i => i.AssetAssignments)
                .SingleAsync(o => o.OrderCode == "RENT-TEST");
            orderA = a.OrderId;
            a.RentalOrder!.RentalStartAt = database.Now.UtcDateTime.AddDays(1);
            a.RentalOrder.RentalEndAt = day10;
            var itemA = a.RentalOrder.RentalOrderItems.Single();
            (itemA.BillableDays, itemA.DailyPrice, itemA.LineAmount) = (5, 200_000m, 1_000_000m);
            var assignmentA = itemA.AssetAssignments.Single();
            (assignmentA.ReservedFrom, assignmentA.ReservedUntil) = (a.RentalOrder.RentalStartAt, day10);

            context.RentalPriceTiers.AddRange(
                new RentalPriceTier { ProductVariantId = database.VariantId, MinDays = 1, MaxDays = 5, DailyPrice = 200_000m, UpdatedAt = database.Now.UtcDateTime },
                new RentalPriceTier { ProductVariantId = database.VariantId, MinDays = 6, MaxDays = null, DailyPrice = 150_000m, UpdatedAt = database.Now.UtcDateTime });

            var itemB = new RentalOrderItem { RentalOrderItemId = Guid.NewGuid(), ProductVariantId = database.VariantId, Quantity = 1, BillableDays = 2, DailyPrice = 200_000m, LineAmount = 400_000m };
            var b = new Order
            {
                OrderId = Guid.NewGuid(),
                OrderCode = "RENT-B",
                CustomerUserId = database.AdminId,
                OrderType = "Rental",
                TotalAmount = 400_000m,
                SubtotalAmount = 400_000m,
                Status = OrderStatuses.Confirmed,
                PaymentStatus = OrderPaymentStatuses.Paid,
                CreatedAt = database.Now,
                UpdatedAt = database.Now,
                RefundPolicyId = database.PolicyId,
                RentalOrder = new RentalOrder { RentalStartAt = day11, RentalEndAt = day11.AddDays(2), RentalOrderItems = [itemB] }
            };
            context.Orders.Add(b);
            context.AssetAssignments.Add(new AssetAssignment
            {
                AssetAssignmentId = Guid.NewGuid(),
                EquipmentAssetId = assignmentA.EquipmentAssetId,
                RentalOrderItemId = itemB.RentalOrderItemId,
                Status = AssignmentStatuses.Reserved,
                AssignedAt = database.Now.UtcDateTime,
                ReservedFrom = day11,
                ReservedUntil = day11.AddDays(2),
                CreatedAt = database.Now.UtcDateTime
            });
            await context.SaveChangesAsync();
            orderB = b.OrderId;
        }

        // A gia hạn tới ngày 12 chồng lên B: 409, A và B không đổi
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => RunAsync(database,
            s => s.ExtendAsync(database.CustomerId, orderA, new ExtendRentalOrderRequest { NewEndAt = day12, AcceptedExtraAmount = 1_000_000m })));
        Assert.Equal(ErrorCodes.ResourceUnavailable, conflict.ErrorCode);
        Assert.Contains("TENT-001", conflict.Errors!["assetCodes"]);
        await AssertUnchangedAsync(database, orderA, day10, orderB, day11.AddDays(2));

        // Báo giá tới ngày 11: 6 ngày nhảy sang bậc 150.000 → 900.000 < 1.000.000 nên tiền thêm = 0; dùng bậc 1–5 cho ví dụ có tiền
        var quote = await RunAsync(database, s => s.QuoteExtensionAsync(database.CustomerId, orderA, new ExtendRentalQuoteRequest { NewEndAt = day11 }));
        Assert.Equal(0m, quote.ExtraAmount);
        await using (var context = database.Context())
            await context.RentalPriceTiers.Where(t => t.MinDays == 6).ExecuteUpdateAsync(x => x.SetProperty(t => t.DailyPrice, 200_000m));
        quote = await RunAsync(database, s => s.QuoteExtensionAsync(database.CustomerId, orderA, new ExtendRentalQuoteRequest { NewEndAt = day11 }));
        Assert.Equal(200_000m, quote.ExtraAmount);

        // Sai số tiền đã đồng ý: 409, DB không đổi
        var priceChanged = await Assert.ThrowsAsync<ConflictException>(() => RunAsync(database,
            s => s.ExtendAsync(database.CustomerId, orderA, new ExtendRentalOrderRequest { NewEndAt = day11, AcceptedExtraAmount = 150_000m })));
        Assert.Equal(ErrorCodes.RentalExtensionPriceChanged, priceChanged.ErrorCode);
        await AssertUnchangedAsync(database, orderA, day10, orderB, day11.AddDays(2));

        // User khác: 404
        await Assert.ThrowsAsync<NotFoundException>(() => RunAsync(database,
            s => s.QuoteExtensionAsync(database.OtherStaffId, orderA, new ExtendRentalQuoteRequest { NewEndAt = day11 })));

        // Gia hạn tới ngày 11 (trước khi B bắt đầu) thành công
        var result = await RunAsync(database,
            s => s.ExtendAsync(database.CustomerId, orderA, new ExtendRentalOrderRequest { NewEndAt = day11, AcceptedExtraAmount = 200_000m }));
        Assert.Equal(200_000m, result.Adjustment.ExtraAmount);
        await using (var check = database.Context())
        {
            Assert.Equal(day11, (await check.RentalOrders.SingleAsync(r => r.OrderId == orderA)).RentalEndAt);
            var assignmentA = await check.AssetAssignments.SingleAsync(x => x.RentalOrderItem!.RentalOrderId == orderA);
            Assert.Equal(day11, assignmentA.ReservedUntil);
            var itemA = await check.RentalOrderItems.SingleAsync(i => i.RentalOrderId == orderA);
            Assert.Equal((6, 200_000m, 1_200_000m), (itemA.BillableDays, itemA.DailyPrice, itemA.LineAmount));
            var adjustment = await check.RentalOrderAdjustments.SingleAsync();
            Assert.Equal((orderA, day10, day11, 200_000m, database.CustomerId),
                (adjustment.RentalOrderId, adjustment.OldEndAt, adjustment.NewEndAt, adjustment.ExtraAmount, adjustment.RequestedByUserId));
            var charge = await check.RentalCharges.SingleAsync();
            Assert.Equal((RentalChargeTypes.Extension, RentalChargeStatuses.Assessed, 200_000m), (charge.ChargeType, charge.Status, charge.Amount));
            Assert.True(await check.Notifications.AnyAsync(n => n.RecipientUserId == database.CustomerId
                                                                && n.EventKey == $"RentalExtended:{adjustment.RentalOrderAdjustmentId}"));
            var assignmentB = await check.AssetAssignments.SingleAsync(x => x.RentalOrderItem!.RentalOrderId == orderB);
            Assert.Equal((day11, day11.AddDays(2)), (assignmentB.ReservedFrom, assignmentB.ReservedUntil));
        }

        var history = await RunAsync(database, s => s.GetAdjustmentsAsync(database.CustomerId, orderA));
        Assert.Equal("Customer Test", Assert.Single(history).RequestedByName);
        await using (var context = database.Context())
        {
            var adminHistory = await database.Fulfillment(context, new FixedClock(database.Now), database.StaffId, RoleNames.Staff).GetAdjustmentsAsync(orderA);
            Assert.Single(adminHistory);
        }

        // Đơn Completed: 409
        await using (var context = database.Context())
            await context.Orders.Where(o => o.OrderId == orderA).ExecuteUpdateAsync(x => x.SetProperty(o => o.Status, OrderStatuses.Completed));
        var completed = await Assert.ThrowsAsync<ConflictException>(() => RunAsync(database,
            s => s.QuoteExtensionAsync(database.CustomerId, orderA, new ExtendRentalQuoteRequest { NewEndAt = day11.AddHours(1) })));
        Assert.Equal(ErrorCodes.RentalNotExtendable, completed.ErrorCode);
    }

    private static async Task AssertUnchangedAsync(AdminTestDatabase database, Guid orderA, DateTime endA, Guid orderB, DateTime endB)
    {
        await using var check = database.Context();
        Assert.Equal(endA, (await check.RentalOrders.SingleAsync(r => r.OrderId == orderA)).RentalEndAt);
        Assert.Equal(endB, (await check.RentalOrders.SingleAsync(r => r.OrderId == orderB)).RentalEndAt);
        Assert.Equal(endA, (await check.AssetAssignments.SingleAsync(x => x.RentalOrderItem!.RentalOrderId == orderA)).ReservedUntil);
        Assert.Equal(endB, (await check.AssetAssignments.SingleAsync(x => x.RentalOrderItem!.RentalOrderId == orderB)).ReservedUntil);
        Assert.Equal(5, (await check.RentalOrderItems.SingleAsync(i => i.RentalOrderId == orderA)).BillableDays);
        Assert.Empty(await check.RentalOrderAdjustments.ToListAsync());
        Assert.Empty(await check.RentalCharges.ToListAsync());
    }

    // Mỗi lần gọi một DbContext mới, giống mỗi request HTTP
    private static async Task<T> RunAsync<T>(AdminTestDatabase database, Func<RentalOrderService, Task<T>> action)
    {
        await using var context = database.Context();
        var unitOfWork = new UnitOfWork(context);
        var clock = new FixedClock(database.Now);
        var service = new RentalOrderService(
            unitOfWork,
            new RentalAvailabilityService(unitOfWork, clock),
            new PromotionService(unitOfWork),
            clock,
            new NotificationService(unitOfWork, TestProxy.Create<ICurrentUserService>(), clock));
        return await action(service);
    }

    private static RentalPriceTierRow Tier(int minDays, int? maxDays, decimal dailyPrice)
        => new() { Id = Guid.NewGuid(), VariantId = VariantId, MinDays = minDays, MaxDays = maxDays, DailyPrice = dailyPrice };

    private static RentalOrderDetailRow Detail(string status, DateTime start, DateTime end) => new()
    {
        OrderId = OrderId,
        OrderCode = "RT-TEST",
        OrderType = OrderTypes.Rental,
        Status = status,
        PaymentStatus = OrderPaymentStatuses.Paid,
        CreatedAt = Now.AddDays(-5),
        TotalAmount = 200_000m,
        SubtotalAmount = 300_000m,
        DiscountAmount = 100_000m,
        RentalStartAt = start,
        RentalEndAt = end,
        CustomerName = "Khách Test",
        Lines =
        [
            new RentalOrderLineRow
            {
                ItemId = ItemId,
                VariantId = VariantId,
                ProductName = "Lều",
                VariantName = "2 người",
                Quantity = 1,
                DailyPrice = 100_000m,
                BillableDays = 3,
                LineAmount = 300_000m
            }
        ],
        Assignments =
        [
            new RentalOrderAssignmentRow
            {
                AssignmentId = AssignmentId,
                RentalOrderItemId = ItemId,
                EquipmentAssetId = AssetId,
                Status = AssignmentStatuses.Issued,
                AssetCode = "TENT-001",
                ReservedFrom = start,
                ReservedUntil = end
            },
            new RentalOrderAssignmentRow
            {
                AssignmentId = Guid.NewGuid(),
                RentalOrderItemId = ItemId,
                EquipmentAssetId = Guid.NewGuid(),
                Status = AssignmentStatuses.Cancelled,
                AssetCode = "TENT-OLD",
                ReservedFrom = start,
                ReservedUntil = end
            }
        ]
    };

    private sealed class Harness
    {
        public int BeginCount { get; private set; }
        public int SaveCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public bool FulfillmentLocked { get; private set; }
        public List<Guid> LockedVariants { get; } = [];
        public AssetAvailabilityFilter? LastFilter { get; private set; }
        public List<Guid> LastExcludedAssignmentIds { get; private set; } = [];
        public List<Guid> LastCheckedAssetIds { get; private set; } = [];
        public Guid? LastAdjustmentsCustomerId { get; private set; } = Guid.Empty;
        public List<RentalOrderAdjustment> Adjustments { get; } = [];
        public List<RentalCharge> Charges { get; } = [];
        public List<Payment> Payments { get; } = [];
        public List<(Guid Recipient, string Type, string EventKey)> Notifications { get; } = [];
        public Order Tracked { get; }
        public IUnitOfWork UnitOfWork { get; }
        public RentalOrderService Service { get; }

        public Harness(
            RentalOrderDetailRow? detail = null,
            IReadOnlyList<RentalPriceTierRow>? tiers = null,
            IReadOnlyList<Guid>? blockedAssetIds = null,
            List<RentalOrderAdjustmentRow>? adjustments = null)
        {
            var owned = detail ?? Detail(OrderStatuses.InFulfillment, StartUtc, EndUtc);
            tiers ??= [Tier(1, 3, 100_000m), Tier(4, null, 80_000m)];
            blockedAssetIds ??= [];

            var item = new RentalOrderItem
            {
                RentalOrderItemId = ItemId,
                RentalOrderId = OrderId,
                ProductVariantId = VariantId,
                Quantity = 1,
                BillableDays = 3,
                DailyPrice = 100_000m,
                LineAmount = 300_000m
            };
            item.AssetAssignments.Add(new AssetAssignment
            {
                AssetAssignmentId = AssignmentId,
                RentalOrderItemId = ItemId,
                EquipmentAssetId = AssetId,
                EquipmentAsset = new EquipmentAsset { EquipmentAssetId = AssetId, ProductVariantId = VariantId, AssetCode = "TENT-001" },
                Status = AssignmentStatuses.Issued,
                ReservedFrom = StartUtc,
                ReservedUntil = EndUtc
            });
            Tracked = new Order
            {
                OrderId = OrderId,
                CustomerUserId = CustomerId,
                OrderCode = "RT-TEST",
                TotalAmount = 200_000m,
                Status = OrderStatuses.InFulfillment,
                RentalOrder = new RentalOrder { OrderId = OrderId, RentalStartAt = StartUtc, RentalEndAt = EndUtc, RentalOrderItems = [item] }
            };

            var rentalOrders = TestProxy.Create<IRentalOrderRepository>(
                ("GetOwnedDetailAsync", args => Task.FromResult((Guid)args[0]! == OrderId && (Guid)args[1]! == CustomerId ? owned : null)),
                ("AcquireFulfillmentLockAsync", _ => { FulfillmentLocked = true; return Task.CompletedTask; }
            ),
                ("GetForFulfillmentAsync", args => Task.FromResult<Order?>((Guid)args[0]! == OrderId ? Tracked : null)),
                ("AddAdjustmentAsync", args => { Adjustments.Add((RentalOrderAdjustment)args[0]!); return Task.CompletedTask; }
            ),
                ("GetPendingAdjustmentAsync", _ => Task.FromResult<RentalOrderAdjustment?>(null)),
                ("GetAdjustmentsAsync", args =>
                {
                    LastAdjustmentsCustomerId = (Guid?)args[1];
                    var visible = (Guid)args[0]! == OrderId && (args[1] is null || (Guid)args[1]! == CustomerId);
                    return Task.FromResult(visible ? adjustments ?? [] : null);
                }
            ));
            var priceTiers = TestProxy.Create<IRentalPriceTierRepository>(
                ("GetForVariantsAsync", _ => Task.FromResult(tiers.ToList())),
                ("GetForBundlesAsync", _ => Task.FromResult(new List<RentalPriceTierRow>())));
            var assets = TestProxy.Create<IEquipmentAssetRepository>(
                ("GetBlockedAssetIdsAsync", args =>
                {
                    LastCheckedAssetIds = ((IReadOnlyCollection<Guid>)args[0]!).ToList();
                    LastExcludedAssignmentIds = ((IReadOnlyCollection<Guid>)args[1]!).ToList();
                    LastFilter = (AssetAvailabilityFilter)args[2]!;
                    return Task.FromResult(blockedAssetIds.ToList());
                }
            ));
            var charges = TestProxy.Create<IRentalChargeRepository>(
                ("AddAsync", args => { Charges.Add((RentalCharge)args[0]!); return Task.CompletedTask; }
            ));

            var policies = TestProxy.Create<IRentalPolicyRepository>(
                ("GetActiveWithTiersAsync", _ => Task.FromResult<RentalPolicy?>(null)),
                ("GetWithTiersAsync", _ => Task.FromResult<RentalPolicy?>(null)));
            var refundCases = TestProxy.Create<IRefundCaseRepository>(
                ("GetLatestByOrderAsync", _ => Task.FromResult<RefundCase?>(null)));
            var settlements = TestProxy.Create<IRentalSettlementRepository>(
                ("GetByOrderAsync", _ => Task.FromResult<RentalSettlement?>(null)));
            var photos = TestProxy.Create<IRentalEvidencePhotoRepository>(
                ("GetByOrderAsync", _ => Task.FromResult<IReadOnlyList<RentalEvidencePhoto>>([])));
            var payments = TestProxy.Create<IPaymentRepository>(
                ("AddAsync", args => { Payments.Add((Payment)args[0]!); return Task.CompletedTask; }));

            UnitOfWork = TestProxy.Create<IUnitOfWork>(
                ("get_RentalOrders", _ => rentalOrders),
                ("get_RentalPolicies", _ => policies),
                ("get_RefundCases", _ => refundCases),
                ("get_RentalSettlements", _ => settlements),
                ("get_RentalEvidencePhotos", _ => photos),
                ("get_Payments", _ => payments),
                ("get_RentalPriceTiers", _ => priceTiers),
                ("get_EquipmentAssets", _ => assets),
                ("get_RentalCharges", _ => charges),
                ("BeginTransactionAsync", _ => { BeginCount++; return Task.CompletedTask; }
            ),
                ("SaveChangesAsync", _ => { SaveCount++; return Task.FromResult(1); }
            ),
                ("CommitAsync", _ => { CommitCount++; return Task.CompletedTask; }
            ),
                ("RollbackAsync", _ => { RollbackCount++; return Task.CompletedTask; }
            ));
            var availability = TestProxy.Create<IRentalAvailabilityService>(
                ("LockVariantsAsync", args => { LockedVariants.AddRange((IEnumerable<Guid>)args[0]!); return Task.CompletedTask; }
            ));
            var notifications = TestProxy.Create<INotificationService>(
                ("QueueAsync", args => { Notifications.Add(((Guid)args[0]!, (string)args[1]!, (string)args[6]!)); return Task.CompletedTask; }
            ));

            Service = new RentalOrderService(UnitOfWork, availability, null!, new FixedClock(Now), notifications);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
