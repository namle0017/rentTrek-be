using RenTrek.BLL.Constants;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class TripBookingExpiryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExpireAsync_OverdueUnpaidOrder_ExpiresOnceAndReleasesEverything()
    {
        var order = CreateOrder(Now.AddMinutes(-1));
        var payment = new Payment { PaymentId = Guid.NewGuid(), OrderId = order.OrderId, Status = PaymentStatuses.Pending };
        var promotion = new AppliedPromotion { Status = AppliedPromotionStatuses.Reserved };
        var invitation = new ParticipantInvitation();
        var service = CreateService(order, payment, [promotion], [invitation]);

        Assert.True(await service.ExpireAsync(order.OrderId));

        Assert.Equal(OrderStatuses.Expired, order.Status);
        Assert.Equal(PaymentStatuses.Cancelled, payment.Status);
        Assert.All(order.TripBookingOrder!.Participants, p => Assert.Equal(BookingParticipantStatuses.Cancelled, p.Status));
        Assert.Equal(AppliedPromotionStatuses.Reversed, promotion.Status);
        Assert.Equal(Now, invitation.RevokedAt);

        // Idempotent: chạy lại không làm gì
        Assert.False(await service.ExpireAsync(order.OrderId));
    }

    [Theory]
    [InlineData(OrderPaymentStatuses.NeedsReconciliation, -1)] // tiền đã về, chờ admin đối soát
    [InlineData(OrderPaymentStatuses.Unpaid, 5)] // còn hạn giữ chỗ
    public async Task ExpireAsync_OrderWithMoneyOrStillHeld_IsLeftUntouched(string paymentStatus, int holdOffsetMinutes)
    {
        var order = CreateOrder(Now.AddMinutes(holdOffsetMinutes));
        order.PaymentStatus = paymentStatus;
        var initialParticipantStatus = order.TripBookingOrder!.Participants.First().Status;
        var payment = new Payment { PaymentId = Guid.NewGuid(), OrderId = order.OrderId, Status = PaymentStatuses.Pending };
        var service = CreateService(order, payment, [], []);

        Assert.False(await service.ExpireAsync(order.OrderId));

        Assert.Equal(OrderStatuses.PendingPayment, order.Status);
        Assert.Equal(PaymentStatuses.Pending, payment.Status);
        Assert.All(order.TripBookingOrder.Participants, p => Assert.Equal(initialParticipantStatus, p.Status));
    }

    private static Order CreateOrder(DateTimeOffset holdExpiresAt)
    {
        var orderId = Guid.NewGuid();
        return new Order
        {
            OrderId = orderId,
            OrderType = OrderTypes.TripBooking,
            Status = OrderStatuses.PendingPayment,
            PaymentStatus = OrderPaymentStatuses.Unpaid,
            HoldExpiresAt = holdExpiresAt,
            TripBookingOrder = new TripBookingOrder
            {
                OrderId = orderId,
                Participants =
                [
                    new BookingParticipant { ParticipantId = Guid.NewGuid() },
                    new BookingParticipant { ParticipantId = Guid.NewGuid() }
                ]
            }
        };
    }

    private static TripBookingExpiryService CreateService(
        Order order,
        Payment payment,
        List<AppliedPromotion> promotions,
        List<ParticipantInvitation> invitations)
    {
        var uow = TestProxy.Create<IUnitOfWork>(
            ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                ("GetLatestPendingCollectionAsync", _ => Task.FromResult<Payment?>(payment.Status == PaymentStatuses.Pending ? payment : null)))),
            ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                ("GetBookingAsync", _ => Task.FromResult<Order?>(order)))),
            ("get_Orders", _ => TestProxy.Create<IOrderRepository>(
                ("GetAppliedPromotionsAsync", _ => Task.FromResult(promotions)),
                ("GetOpenInvitationsForUpdateAsync", _ => Task.FromResult(invitations)))),
            ("SaveChangesAsync", _ => Task.FromResult(1)));
        return new TripBookingExpiryService(uow, new FixedClock(Now));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
