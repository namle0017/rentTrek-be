using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class RentalCancellationTests
{
    private static (RentalOrderService Service, Order Order) Fixture()
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(), CustomerUserId = Guid.NewGuid(), OrderType = "Rental",
            Status = "PendingPayment", PaymentStatus = "Unpaid", HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            RentalOrder = new RentalOrder { RentalOrderItems = [new RentalOrderItem { AssetAssignments = [new AssetAssignment { Status = "Reserved" }] }] },
            AppliedPromotions = [new AppliedPromotion { Status = "Applied", AssetAssignments = [new AssetAssignment { Status = "Reserved" }] }],
            Payments = [new Payment { Status = "Pending" }]
        };
        var repository = TestProxy.Create<IRentalOrderRepository>(
            ("GetForFulfillmentAsync", _ => Task.FromResult<Order?>(order)),
            ("GetOwnedDetailAsync", _ => Task.FromResult<RentalOrderDetailRow?>(new RentalOrderDetailRow
            {
                OrderId = order.OrderId, OrderType = "Rental", Status = order.Status, PaymentStatus = order.PaymentStatus,
                CreatedAt = DateTimeOffset.UtcNow, RentalStartAt = DateTime.UtcNow.AddDays(1), RentalEndAt = DateTime.UtcNow.AddDays(2)
            })));
        var unit = TestProxy.Create<IUnitOfWork>(
            ("get_RentalOrders", _ => repository),
            ("get_Payments", _ => TestProxy.Create<IPaymentRepository>()),
            ("SaveChangesAsync", _ => Task.FromResult(1)));
        return (new RentalOrderService(unit, null!, null!, TimeProvider.System), order);
    }

    [Fact]
    public async Task CancelUnpaid_ReleasesInventoryAndPromotionAndPendingPayment_Idempotently()
    {
        var (service, order) = Fixture();
        var first = await service.CancelUnpaidAsync(order.CustomerUserId, order.OrderId, new CancelRentalOrderRequest { Reason = "Changed plans" });
        var second = await service.CancelUnpaidAsync(order.CustomerUserId, order.OrderId, new CancelRentalOrderRequest());
        Assert.Equal("Cancelled", first.Status);
        Assert.Equal(first.Id, second.Id);
        Assert.Null(order.HoldExpiresAt);
        Assert.Equal("Cancelled", order.RentalOrder!.RentalOrderItems.Single().AssetAssignments.Single().Status);
        Assert.Equal("Reversed", order.AppliedPromotions.Single().Status);
        Assert.Equal("Cancelled", order.AppliedPromotions.Single().AssetAssignments.Single().Status);
        Assert.Equal("Cancelled", order.Payments.Single().Status);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("NeedsReconciliation")]
    public async Task CancelUnpaid_RejectsOrdersWithMoneyOrReconciliation(string paymentStatus)
    {
        var (service, order) = Fixture();
        order.PaymentStatus = paymentStatus;
        await Assert.ThrowsAsync<ConflictException>(() => service.CancelUnpaidAsync(order.CustomerUserId, order.OrderId, new CancelRentalOrderRequest()));
        Assert.Equal("Reserved", order.RentalOrder!.RentalOrderItems.Single().AssetAssignments.Single().Status);
    }

    [Fact]
    public async Task CancelUnpaid_DoesNotExposeAnotherCustomersOrder()
    {
        var (service, order) = Fixture();
        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelUnpaidAsync(Guid.NewGuid(), order.OrderId, new CancelRentalOrderRequest()));
        Assert.Equal("PendingPayment", order.Status);
    }
}
