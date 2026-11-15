using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class TripRescheduleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CheaperTrip_SwitchesNowAndRefundsTheDifference()
    {
        var f = new Fixture(toPrice: 800_000m);

        var result = await f.Service.RescheduleAsync(f.Order.OrderId, new RescheduleTripBookingRequest { ToTripId = f.To.TripId });

        Assert.Equal("Completed", result.Status);
        Assert.Equal(-350_000m, result.PriceDiff); // (800k − 1M) × 2 + 50k phí
        Assert.Equal(350_000m, f.RefundedAmount);
        Assert.Equal(f.To.TripId, f.Order.TripBookingOrder.TripId);
        Assert.Equal(800_000m, f.Order.TripBookingOrder.UnitPrice);
        Assert.Equal(1, f.Order.TripBookingOrder.RescheduleCount);
        Assert.All(f.Order.TripBookingOrder.Participants, p => Assert.Equal((f.To.TripId, (Guid?)null), (p.TripId, p.TripGroupId)));
        Assert.Equal(1_650_000m, f.Order.TotalAmount);
        Assert.True(f.From.ProvidedItemsNeedsRecalc && f.To.ProvidedItemsNeedsRecalc);
        Assert.True(f.Regrouped);
    }

    [Fact]
    public async Task PricierTrip_HoldsSeatsAndAsksForTheDifference()
    {
        var f = new Fixture(toPrice: 1_200_000m);

        var result = await f.Service.RescheduleAsync(f.Order.OrderId, new RescheduleTripBookingRequest { ToTripId = f.To.TripId });

        Assert.Equal("PendingPayment", result.Status);
        Assert.Equal(Now.AddMinutes(15), result.HoldExpiresAt);
        var payment = Assert.Single(f.Payments);
        Assert.Equal((450_000m, PaymentPurposes.TripReschedule, PaymentStatuses.Pending), (payment.Amount, payment.Purpose, payment.Status));
        Assert.Equal(f.From.TripId, f.Order.TripBookingOrder.TripId);
        Assert.Null(f.RefundedAmount);
    }

    [Fact]
    public async Task PolicyWithoutRescheduleTerms_BlocksIt()
    {
        var f = new Fixture(toPrice: 1_000_000m);
        f.Policy.RescheduleMinHoursBefore = null;

        var options = await f.Service.GetOptionsAsync(f.Order.OrderId);

        Assert.False(options.CanReschedule);
        Assert.Empty(options.Trips);
        await Assert.ThrowsAsync<ConflictException>(() =>
            f.Service.RescheduleAsync(f.Order.OrderId, new RescheduleTripBookingRequest { ToTripId = f.To.TripId }));
    }

    [Fact]
    public async Task Options_SkipTripsWithoutEnoughSeats()
    {
        var f = new Fixture(toPrice: 1_000_000m);
        f.Reserved[f.To.TripId] = 9; // 10 chỗ, còn 1 < 2 khách

        var options = await f.Service.GetOptionsAsync(f.Order.OrderId);

        Assert.True(options.CanReschedule);
        Assert.Empty(options.Trips);
    }

    [Fact]
    public async Task PaidDifference_CompletesTheSwitch_CountingItsOwnHoldAsFree()
    {
        var f = new Fixture(toPrice: 1_200_000m);
        var (reschedule, payment) = f.SeedPending(450_000m);
        f.Reserved[f.To.TripId] = 10; // 8 chỗ khác + 2 chỗ đang giữ cho chính đơn này

        await ((ITripReschedulePaymentHandler)f.Service).ApplySucceededPaymentAsync(payment, Now);

        Assert.Equal("Completed", reschedule.Status);
        Assert.Equal(f.To.TripId, f.Order.TripBookingOrder.TripId);
        Assert.Null(f.RefundedAmount);
    }

    [Fact]
    public async Task PaidDifference_TargetFull_RefundsThePayment()
    {
        var f = new Fixture(toPrice: 1_200_000m);
        var (reschedule, payment) = f.SeedPending(450_000m, holdExpiresAt: Now.AddMinutes(-1));
        f.Reserved[f.To.TripId] = 10;

        await ((ITripReschedulePaymentHandler)f.Service).ApplySucceededPaymentAsync(payment, Now);

        Assert.Equal("Failed", reschedule.Status);
        Assert.Equal(f.From.TripId, f.Order.TripBookingOrder.TripId);
        Assert.Equal(450_000m, f.RefundedAmount);
    }

    [Fact]
    public async Task CompanyCancelledTrip_MovesForFree_AndDropsTheFullRefund()
    {
        var f = new Fixture(toPrice: 1_200_000m);
        f.Policy.RescheduleMinHoursBefore = null; // đổi lịch thường bị chặn, ca công ty huỷ vẫn được
        f.From.Status = TripStatuses.Cancelled;
        f.Order.Status = OrderStatuses.Cancelled;
        f.Order.CancelledAt = Now.AddHours(-1);
        foreach (var p in f.Order.TripBookingOrder.Participants)
            (p.Status, p.UpdatedAt) = (BookingParticipantStatuses.Cancelled, Now.AddHours(-1));
        f.CancelRefund = new RefundCase
        {
            RefundCaseId = Guid.NewGuid(),
            Source = RefundCaseSources.TripCancellation,
            Status = RefundCaseStatuses.AwaitingBankInfo,
            RefundPayment = new Payment { Status = PaymentStatuses.PendingApproval }
        };

        var options = await f.Service.GetOptionsAsync(f.Order.OrderId);
        var result = await f.Service.RescheduleAsync(f.Order.OrderId, new RescheduleTripBookingRequest { ToTripId = f.To.TripId });

        Assert.True(options.CompanyCancelled && options.CanReschedule);
        Assert.Equal(0m, Assert.Single(options.Trips).PriceDiff); // chuyến đắt hơn: không thu thêm
        Assert.Equal(("Completed", 0m), (result.Status, result.PriceDiff));
        Assert.Equal(OrderStatuses.Confirmed, f.Order.Status);
        Assert.Null(f.Order.CancelledAt);
        Assert.Equal(0, f.Order.TripBookingOrder.RescheduleCount);
        Assert.All(f.Order.TripBookingOrder.Participants, p => Assert.Equal(BookingParticipantStatuses.Pending, p.Status));
        Assert.Equal((RefundCaseStatuses.Cancelled, PaymentStatuses.Cancelled), (f.CancelRefund.Status, f.CancelRefund.RefundPayment.Status));
        Assert.Null(f.RefundedAmount);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture
    {
        public Fixture(decimal toPrice)
        {
            var userId = Guid.NewGuid();
            var tourId = Guid.NewGuid();
            From = new Trip { TripId = Guid.NewGuid(), TourId = tourId, Status = TripStatuses.Open, StartAt = Now.AddDays(10), TotalSlots = 10, PricePerPerson = 1_000_000m };
            To = new Trip { TripId = Guid.NewGuid(), TourId = tourId, Status = TripStatuses.Open, StartAt = Now.AddDays(20), TotalSlots = 10, PricePerPerson = toPrice };
            Policy = new RefundPolicy { RescheduleMinHoursBefore = 72, MaxReschedules = 1, RescheduleFee = 50_000m };
            Order = new Order
            {
                OrderId = Guid.NewGuid(),
                OrderCode = "TB-1",
                CustomerUserId = userId,
                Status = OrderStatuses.Confirmed,
                PaymentStatus = OrderPaymentStatuses.Paid,
                TotalAmount = 2_000_000m,
                SubtotalAmount = 2_000_000m,
                TripBookingOrder = new TripBookingOrder
                {
                    TripId = From.TripId,
                    Trip = From,
                    ParticipantCount = 2,
                    UnitPrice = 1_000_000m,
                    Participants = Enumerable.Range(0, 2)
                        .Select(_ => new BookingParticipant { TripId = From.TripId, TripGroupId = Guid.NewGuid() })
                        .ToList()
                }
            };
            var trips = new Dictionary<Guid, Trip> { [From.TripId] = From, [To.TripId] = To };

            var unitOfWork = TestProxy.Create<IUnitOfWork>(
                ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                    ("GetOwnedBookingAsync", _ => Task.FromResult<Order?>(Order)),
                    ("GetBookingAsync", _ => Task.FromResult<Order?>(Order)),
                    ("GetTripForUpdateAsync", args => Task.FromResult<Trip?>(trips[(Guid)args[0]!])),
                    ("GetReservedParticipantCountAsync", args => Task.FromResult(Reserved.GetValueOrDefault((Guid)args[0]!))))),
                ("get_RefundCases", _ => TestProxy.Create<IRefundCaseRepository>(
                    ("GetPolicyWithRulesAsync", _ => Task.FromResult<RefundPolicy?>(Policy)),
                    ("GetForUpdateAsync", _ => Task.FromResult(CancelRefund)))),
                ("get_TripReschedules", _ => TestProxy.Create<ITripRescheduleRepository>(
                    ("AddAsync", args => { Reschedules.Add((TripBookingReschedule)args[0]!); return Task.CompletedTask; }),
                    ("GetPendingByOrderAsync", _ => Task.FromResult<IReadOnlyList<TripBookingReschedule>>(
                        Reschedules.Where(x => x.Status == "PendingPayment").ToList())),
                    ("GetByPaymentIdAsync", args => Task.FromResult(Reschedules.SingleOrDefault(x => x.PaymentId == (Guid)args[0]!))),
                    ("GetOpenSiblingTripsAsync", _ => Task.FromResult<IReadOnlyList<Trip>>([To])))),
                ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                    ("AddAsync", args => { Payments.Add((Payment)args[0]!); return Task.CompletedTask; }),
                    ("GetByIdAsync", args => Task.FromResult(Payments.SingleOrDefault(x => x.PaymentId == (Guid)args[0]!))))),
                ("get_TripGroups", _ => TestProxy.Create<ITripGroupRepository>()),
                ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>()),
                ("SaveChangesAsync", _ => Task.FromResult(1)));

            var opener = TestProxy.Create<IRentalRefundOpener>(("OpenRentalRefundAsync", args =>
            {
                RefundedAmount = (decimal)args[1]!;
                return Task.FromResult(new RefundCaseSummaryResponse());
            }));
            var grouping = TestProxy.Create<ITripGroupingService>(("AutoAssignPaidBookingInTransactionAsync", _ =>
            {
                Regrouped = true;
                return Task.CompletedTask;
            }));

            Service = new TripRescheduleService(
                unitOfWork,
                TestProxy.Create<ICurrentUserService>(("get_RequiredUserId", _ => userId)),
                new FixedTime(),
                TestProxy.Create<IRefundCaseService>(("GetSummaryByOrderAsync", _ => Task.FromResult(CancelRefund is null
                    ? null
                    : new RefundCaseSummaryResponse { RefundCaseId = CancelRefund.RefundCaseId, Source = CancelRefund.Source, Status = CancelRefund.Status }))),
                opener,
                grouping,
                TestProxy.Create<INotificationService>());
        }

        public Trip From { get; }
        public Trip To { get; }
        public RefundPolicy Policy { get; }
        public Order Order { get; }
        public TripRescheduleService Service { get; }
        public Dictionary<Guid, int> Reserved { get; } = [];
        public List<TripBookingReschedule> Reschedules { get; } = [];
        public List<Payment> Payments { get; } = [];
        public RefundCase? CancelRefund { get; set; }
        public decimal? RefundedAmount { get; private set; }
        public bool Regrouped { get; private set; }

        public (TripBookingReschedule, Payment) SeedPending(decimal amount, DateTimeOffset? holdExpiresAt = null)
        {
            var payment = new Payment { PaymentId = Guid.NewGuid(), OrderId = Order.OrderId, Amount = amount, Status = PaymentStatuses.Succeeded };
            var reschedule = new TripBookingReschedule
            {
                Id = Guid.NewGuid(),
                OrderId = Order.OrderId,
                FromTripId = From.TripId,
                ToTripId = To.TripId,
                ParticipantCount = 2,
                PriceDiff = amount,
                Status = "PendingPayment",
                PaymentId = payment.PaymentId,
                HoldExpiresAt = holdExpiresAt ?? Now.AddMinutes(10)
            };
            Payments.Add(payment);
            Reschedules.Add(reschedule);
            return (reschedule, payment);
        }
    }
}
