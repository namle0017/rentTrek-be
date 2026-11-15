using RenTrek.BLL.Services.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.DTOs.Promotion.Internal;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Rental.Internal;
using RenTrek.BLL.DTOs.Rental.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class NotificationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueueAsync_AddsWithoutSaving_AndDeduplicatesPerRecipientAndEventKey()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var harness = new NotificationHarness(userA);

        await harness.Service.QueueAsync(
            userA, NotificationTypes.OrderConfirmed, "Xác nhận", "Đơn RT-1 đã xác nhận", "Order", orderId, $"OrderConfirmed:{orderId}");
        await harness.Service.QueueAsync(
            userA, NotificationTypes.OrderConfirmed, "Xác nhận", "Đơn RT-1 đã xác nhận", "Order", orderId, $"OrderConfirmed:{orderId}");
        await harness.Service.QueueAsync(
            userB, NotificationTypes.TripCancelled, "Hủy chuyến", "Chuyến đi bị hủy", "Trip", orderId, $"TripCancelled:{orderId}");

        Assert.Equal(0, harness.SaveChangesCalls);
        Assert.Equal(2, harness.Repo.Items.Count);
        Assert.Contains(harness.Repo.Items, n => n.RecipientUserId == userA && n.EventKey == $"OrderConfirmed:{orderId}");
        Assert.Contains(harness.Repo.Items, n => n.RecipientUserId == userB && n.EventKey == $"TripCancelled:{orderId}");
    }

    [Fact]
    public async Task GetMyNotificationsAndUnreadCount_IsolatesUsersOrdersNewestFirstAndFiltersUnread()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var harness = new NotificationHarness(userA);
        harness.Repo.Items.AddRange(
        [
            new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientUserId = userA,
                NotificationType = NotificationTypes.OrderPlaced,
                Title = "Cũ đã đọc",
                Content = "Nội dung 1",
                CreatedAt = Now.UtcDateTime.AddHours(-2),
                ReadAt = Now.UtcDateTime.AddHours(-1)
            },
            new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientUserId = userA,
                NotificationType = NotificationTypes.OrderConfirmed,
                Title = "Mới chưa đọc",
                Content = "Nội dung 2",
                CreatedAt = Now.UtcDateTime.AddMinutes(-10),
                ReadAt = null
            },
            new Notification
            {
                NotificationId = Guid.NewGuid(),
                RecipientUserId = userB,
                NotificationType = NotificationTypes.OrderPlaced,
                Title = "Của B",
                Content = "Không được lộ sang A",
                CreatedAt = Now.UtcDateTime,
                ReadAt = null
            }
        ]);

        var allA = await harness.Service.GetMyNotificationsAsync(null, page: 1, pageSize: 10);
        var unreadA = await harness.Service.GetMyNotificationsAsync(true, page: 1, pageSize: 10);
        var countA = await harness.Service.GetUnreadCountAsync();

        Assert.Equal(2, allA.TotalItems);
        Assert.Equal(["Mới chưa đọc", "Cũ đã đọc"], allA.Items.Select(x => x.Title).ToArray());
        Assert.All(allA.Items, item => Assert.DoesNotContain("Của B", item.Title));

        var singleUnread = Assert.Single(unreadA.Items);
        Assert.Equal("Mới chưa đọc", singleUnread.Title);
        Assert.False(singleUnread.IsRead);
        Assert.Equal(1, countA.UnreadCount);

        harness.ActAs(userB);
        var allB = await harness.Service.GetMyNotificationsAsync(null, page: 1, pageSize: 10);
        Assert.Single(allB.Items);
        Assert.Equal("Của B", allB.Items[0].Title);
    }

    [Fact]
    public async Task MarkAsRead_SetsReadAtOnce_AndRejectsOtherUsersWith404()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var harness = new NotificationHarness(userA);
        var notificationId = Guid.NewGuid();
        harness.Repo.Items.Add(new Notification
        {
            NotificationId = notificationId,
            RecipientUserId = userA,
            NotificationType = NotificationTypes.OrderConfirmed,
            Title = "Đã xác nhận",
            Content = "Đơn RT-1",
            CreatedAt = Now.UtcDateTime.AddMinutes(-5)
        });

        harness.ActAs(userB);
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.MarkAsReadAsync(notificationId));

        harness.ActAs(userA);
        var firstRead = await harness.Service.MarkAsReadAsync(notificationId);
        Assert.True(firstRead.IsRead);
        Assert.Equal(Now.UtcDateTime, firstRead.ReadAt);

        harness.Clock.Current = Now.AddMinutes(30);
        var secondRead = await harness.Service.MarkAsReadAsync(notificationId);
        Assert.Equal(Now.UtcDateTime, secondRead.ReadAt);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksOnlyCurrentUsersUnreadAndReturnsCount()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var harness = new NotificationHarness(userA);
        var alreadyReadAt = Now.UtcDateTime.AddHours(-3);
        harness.Repo.Items.AddRange(
        [
            new Notification { NotificationId = Guid.NewGuid(), RecipientUserId = userA, NotificationType = NotificationTypes.OrderPlaced, Title = "1", Content = "c", CreatedAt = Now.UtcDateTime },
            new Notification { NotificationId = Guid.NewGuid(), RecipientUserId = userA, NotificationType = NotificationTypes.OrderConfirmed, Title = "2", Content = "c", CreatedAt = Now.UtcDateTime },
            new Notification { NotificationId = Guid.NewGuid(), RecipientUserId = userA, NotificationType = NotificationTypes.OrderCancelled, Title = "3", Content = "c", CreatedAt = Now.UtcDateTime, ReadAt = alreadyReadAt },
            new Notification { NotificationId = Guid.NewGuid(), RecipientUserId = userB, NotificationType = NotificationTypes.OrderPlaced, Title = "B", Content = "c", CreatedAt = Now.UtcDateTime }
        ]);

        var result = await harness.Service.MarkAllAsReadAsync();

        Assert.Equal(2, result.MarkedCount);
        Assert.Equal(alreadyReadAt, harness.Repo.Items[2].ReadAt);
        Assert.Null(harness.Repo.Items[3].ReadAt);
    }

    [Fact]
    public async Task TripBooking_CreateAndCancel_QueuesOrderPlacedAndOrderCancelledForBuyer()
    {
        var buyerId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var tour = new Tour { Id = Guid.NewGuid(), Name = "Tà Năng Phan Dũng", Status = "Active" };
        var trip = new Trip
        {
            TripId = tripId,
            TourId = tour.Id,
            Tour = tour,
            RefundPolicyId = Guid.NewGuid(),
            PricePerPerson = 1_500_000m,
            TotalSlots = 10,
            Status = TripStatuses.Open,
            StartAt = Now.AddDays(5),
            EndAt = Now.AddDays(7)
        };
        var harness = new NotificationHarness(buyerId);
        Order? savedOrder = null;

        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_Tours", _ => TestProxy.Create<ITourRepository>(("GetByIdAsync", _ => Task.FromResult<Tour?>(tour)))),
            ("get_RefundPolicies", _ => TestProxy.Create<IRefundPolicyRepository>(
                ("GetDetailAsync", _ => Task.FromResult<RefundPolicy?>(new RefundPolicy
                {
                    RefundPolicyId = trip.RefundPolicyId,
                    PolicyType = RefundPolicyTypes.Tour,
                    Status = RefundPolicyStatuses.Active,
                    Version = 1
                })))),
            ("get_TripGroups", _ => TestProxy.Create<ITripGroupRepository>(("AcquireTripLockAsync", _ => Task.CompletedTask))),
            ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                ("AcquireOrderLockAsync", _ => Task.CompletedTask),
                ("GetLatestPendingCollectionAsync", _ => Task.FromResult<Payment?>(null)))),
            ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", _ => Task.CompletedTask))),
            ("get_Notifications", _ => harness.Repo),
            ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                ("GetAccountAsync", _ => Task.FromResult<Account?>(new Account
                {
                    UserId = buyerId,
                    FullName = "Khách",
                    Email = "khach@example.com",
                    Phone = "0901234567",
                    Status = AccountStatuses.Active
                })),
                ("GetByIdempotencyKeyAsync", _ => Task.FromResult<Order?>(null)),
                ("AcquireIdempotencyLockAsync", _ => Task.CompletedTask),
                ("AcquireBookingLockAsync", _ => Task.CompletedTask),
                ("GetTripForUpdateAsync", _ => Task.FromResult<Trip?>(trip)),
                ("GetReservedParticipantCountAsync", _ => Task.FromResult(0)),
                ("AddOrderAsync", args => { savedOrder = (Order)args[0]!; return Task.CompletedTask; }
            ),
                ("GetOwnedBookingAsync", _ => Task.FromResult(savedOrder)))));

        var service = new TripBookingService(
            uow,
            harness.CurrentUser,
            harness.Clock,
            TestProxy.Create<IInviteTokenHelper>(("CreateGroupToken", _ => "token")),
            Microsoft.Extensions.Options.Options.Create(new EmailSettings()),
            harness.Service,
            TestProxy.NoRefunds());

        var created = await service.CreateAsync(new CreateTripBookingRequest
        {
            AcceptedRefundPolicyVersion = 1,
            TripId = tripId,
            ContactPhone = "0901234567"
        }, "idem-1");

        var placed = Assert.Single(harness.Repo.Items, n => n.NotificationType == NotificationTypes.OrderPlaced);
        Assert.Equal(buyerId, placed.RecipientUserId);
        Assert.Equal($"OrderPlaced:{created.OrderId}", placed.EventKey);
        Assert.Contains(created.OrderCode, placed.Content);
        Assert.Contains("Tà Năng Phan Dũng", placed.Content);

        await service.CancelAsync(created.OrderId);
        var cancelled = Assert.Single(harness.Repo.Items, n => n.NotificationType == NotificationTypes.OrderCancelled);
        Assert.Equal(buyerId, cancelled.RecipientUserId);
        Assert.Equal($"OrderCancelled:{created.OrderId}", cancelled.EventKey);
        Assert.Contains(created.OrderCode, cancelled.Content);
        Assert.Contains("Tà Năng Phan Dũng", cancelled.Content);
    }

    [Fact]
    public async Task RentalOrder_CreateAsync_QueuesOrderPlacedNotificationForBuyer()
    {
        var buyerId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var harness = new NotificationHarness(buyerId);
        Order? storedOrder = null;

        var startAt = Now.UtcDateTime.AddDays(2);
        var endAt = Now.UtcDateTime.AddDays(4);
        var line = new ResolvedRentalLineResult
        {
            Index = 0,
            ItemType = RentalItemTypes.Variant,
            ProductVariantId = variantId,
            DisplayName = "Lều 2 người",
            Quantity = 1,
            Components =
            [
                new RentalLineComponentItem { ProductVariantId = variantId, VariantName = "Lều 2 người", QuantityPerUnit = 1, TotalQuantity = 1 }
            ]
        };

        var availability = TestProxy.Create<IRentalAvailabilityService>(
            ("ResolveLinesAsync", _ => Task.FromResult(new List<ResolvedRentalLineResult> { line })),
            ("LockVariantsAsync", _ => Task.CompletedTask),
            ("EvaluateAsync", _ => Task.FromResult(new List<RentalLineAvailabilityResult>
            {
                new() { LineIndex = 0, IsAvailable = true, AvailableQuantity = 2 }
            })),
            ("PickAssetsAsync", _ => Task.FromResult(new Dictionary<Guid, List<Guid>> { [variantId] = [assetId] })));

        var promotion = TestProxy.Create<IPromotionService>(
            ("EvaluateAsync", _ => Task.FromResult(new PromotionEvaluationResult { IsApplied = false })));

        var uow = TestProxy.Create<IUnitOfWork>([
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_Notifications", _ => harness.Repo),
            ("get_RentalPriceTiers", _ => TestProxy.Create<IRentalPriceTierRepository>(
                ("GetForVariantsAsync", _ => Task.FromResult(new List<RentalPriceTierRow>
                {
                    new() { Id = Guid.NewGuid(), VariantId = variantId, MinDays = 1, MaxDays = null, DailyPrice = 100_000m }
                })))),
            ("get_RefundPolicies", _ => TestProxy.Create<IRefundPolicyRepository>(
                ("GetCurrentAsync", _ => Task.FromResult<RefundPolicy?>(new RefundPolicy
                {
                    RefundPolicyId = Guid.NewGuid(),
                    Name = "Chính sách thuê",
                    Version = 1,
                    RefundPolicyRules = []
                })))),
            ("get_SystemConfigurations", _ => TestProxy.Create<ISystemConfigurationRepository>(
                ("GetValueAsync", _ => Task.FromResult<string?>("15")))),
            ("get_RentalOrders", _ => TestProxy.Create<IRentalOrderRepository>(
                ("GetByIdempotencyKeyAsync", _ => Task.FromResult<Order?>(null)),
                ("HasOutstandingDebtAsync", _ => Task.FromResult(false)),
                ("AcquireIdempotencyLockAsync", _ => Task.CompletedTask),
                ("OrderCodeExistsAsync", _ => Task.FromResult(false)),
                ("AddOrderAsync", args => { storedOrder = (Order)args[0]!; return Task.CompletedTask; }
            ),
                ("GetOwnedDetailAsync", _ => Task.FromResult<RentalOrderDetailRow?>(new RentalOrderDetailRow
                {
                    OrderId = storedOrder!.OrderId,
                    OrderCode = storedOrder.OrderCode,
                    OrderType = storedOrder.OrderType,
                    Status = storedOrder.Status,
                    PaymentStatus = storedOrder.PaymentStatus,
                    CreatedAt = storedOrder.CreatedAt,
                    HoldExpiresAt = storedOrder.HoldExpiresAt,
                    RentalStartAt = storedOrder.RentalOrder!.RentalStartAt,
                    RentalEndAt = storedOrder.RentalOrder!.RentalEndAt,
                    SubtotalAmount = storedOrder.SubtotalAmount,
                    TotalAmount = storedOrder.TotalAmount,
                    RefundPolicyId = storedOrder.RefundPolicyId,
                    RefundPolicyName = "Chính sách thuê",
                    RefundPolicyVersion = 1
                })))),
            .. RentalDepositTestDefaults.UnitOfWorkHandlers()]);

        var service = new RentalOrderService(uow, availability, promotion, harness.Clock, harness.Service);

        var created = await service.CreateAsync(buyerId, new CreateRentalOrderRequest
        {
            RentalStartAt = startAt,
            RentalEndAt = endAt,
            Items = [new RentalItemRequest { ProductVariantId = variantId, Quantity = 1 }],
            AcceptedRefundPolicyVersion = 1
        }, "rental-idem-1");

        var placed = Assert.Single(harness.Repo.Items);
        Assert.Equal(buyerId, placed.RecipientUserId);
        Assert.Equal(NotificationTypes.OrderPlaced, placed.NotificationType);
        Assert.Equal($"OrderPlaced:{created.Id}", placed.EventKey);
        Assert.Contains(created.OrderCode, placed.Content);
    }

    [Fact]
    public async Task PaymentService_Webhook_DuplicatesProduceSingleOrderConfirmedNotification()
    {
        var buyerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            CustomerUserId = buyerId,
            OrderType = OrderTypes.TripBooking,
            OrderCode = "RT-PAY-01",
            TotalAmount = 1_000_000m,
            Status = OrderStatuses.PendingPayment,
            PaymentStatus = OrderPaymentStatuses.Unpaid,
            HoldExpiresAt = Now.AddMinutes(15),
            TripBookingOrder = new TripBookingOrder
            {
                OrderId = orderId,
                Trip = new Trip { TripId = Guid.NewGuid(), StartAt = Now.AddDays(3), Tour = new Tour { Name = "Bidoup" } },
                Participants = []
            }
        };
        var payments = new List<Payment>();
        var harness = new NotificationHarness(buyerId);
        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_Notifications", _ => harness.Repo),
            ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                ("GetBookingAsync", _ => Task.FromResult<Order?>(order)))),
            ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                ("AcquireOrderLockAsync", _ => Task.CompletedTask),
                ("GetOrderAsync", _ => Task.FromResult<Order?>(order)),
                ("GetLatestPendingCollectionAsync", _ => Task.FromResult(payments.LastOrDefault(p => p.Status == PaymentStatuses.Pending))),
                ("AddAsync", args => { payments.Add((Payment)args[0]!); return Task.CompletedTask; }
            ),
                ("FindByIdPrefixAsync", args => Task.FromResult(payments.FirstOrDefault(p => p.PaymentId.ToString("N")[..16] == (string)args[0]!))),
                ("GetByIdAsync", args => Task.FromResult(payments.SingleOrDefault(p => p.PaymentId == (Guid)args[0]!))),
                ("ProviderTransactionExistsAsync", args => Task.FromResult(payments.Any(p => p.ProviderTransactionId == (string)args[0]!))))));

        var service = new PaymentService(
            uow,
            harness.Clock,
            Microsoft.Extensions.Options.Options.Create(new SePaySettings
            {
                Api = new SePaySettings.ApiSettings { Key = "webhook-key" },
                BankName = "MBBank",
                AccountNumber = "0123456789",
                AccountName = "RENTREK"
            }),
            TestProxy.Create<IEmailSender>(),
            Microsoft.Extensions.Options.Options.Create(new EmailSettings()),
            TestProxy.Create<ITripGroupingService>(),
            harness.Service,
            NullLogger<PaymentService>.Instance);

        var (payment, _) = await service.CreateAsync(buyerId, orderId, new CreatePaymentRequest { PaymentMethod = "BankTransfer" });
        var webhook = new SePayWebhookRequest { Id = 99, TransferType = "in", Content = payment.BankTransfer.TransferContent, TransferAmount = 1_000_000m };

        await service.HandleSePayWebhookAsync("Apikey webhook-key", webhook);
        await service.HandleSePayWebhookAsync("Apikey webhook-key", webhook);

        var notification = Assert.Single(harness.Repo.Items);
        Assert.Equal(buyerId, notification.RecipientUserId);
        Assert.Equal(NotificationTypes.OrderConfirmed, notification.NotificationType);
        Assert.Equal($"OrderConfirmed:{orderId}", notification.EventKey);
        Assert.Contains("RT-PAY-01", notification.Content);
        Assert.DoesNotContain("0123456789", notification.Content);
    }

    [Fact]
    public async Task AdminOrderService_ManualPaymentAndCancel_QueuesOrderConfirmedAndOrderCancelled()
    {
        var adminId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            OrderCode = "RT-ADM-01",
            OrderType = OrderTypes.Rental,
            CustomerUserId = buyerId,
            TotalAmount = 500_000m,
            Status = OrderStatuses.PendingPayment,
            PaymentStatus = OrderPaymentStatuses.Unpaid
        };
        var harness = new NotificationHarness(adminId, RoleNames.Admin);
        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("get_AdminOperations", _ => TestProxy.Create<IAdminOperationRepository>(("AcquireLockAsync", _ => Task.CompletedTask))),
            ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", _ => Task.CompletedTask))),
            ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(("AddAsync", args => { order.Payments.Add((Payment)args[0]!); return Task.CompletedTask; }))),
            ("get_Notifications", _ => harness.Repo),
            // Đơn thuê không có cọc trong test này
            ("get_RentalOrders", _ => TestProxy.Create<IRentalOrderRepository>(("GetByIdAsync", _ => Task.FromResult<RentalOrder?>(null)))),
            ("get_Orders", _ => TestProxy.Create<IOrderRepository>(
                ("GetAdminForUpdateAsync", _ => Task.FromResult<Order?>(order)),
                ("GetManualCollectionAsync", _ => Task.FromResult<Payment?>(null)),
                ("GetAppliedPromotionsAsync", _ => Task.FromResult(new List<AppliedPromotion>())),
                ("GetAssignmentsForUpdateAsync", _ => Task.FromResult(new List<AssetAssignment>())),
                ("GetParticipantsAsync", _ => Task.FromResult(new List<BookingParticipant>())),
                ("GetOpenInvitationsForUpdateAsync", _ => Task.FromResult(new List<ParticipantInvitation>())))));

        var service = new AdminOrderService(uow, harness.CurrentUser, harness.Clock, harness.Service);

        await service.ManualPaymentAsync(order.OrderCode, new ManualPaymentRequest(500_000m, "Cash", "MAN-1", "Thu tiền mặt"), CancellationToken.None);
        await service.CancelOrderAsync(order.OrderCode, new ReasonRequest("Khách yêu cầu hủy"), CancellationToken.None);

        var confirmed = Assert.Single(harness.Repo.Items, n => n.NotificationType == NotificationTypes.OrderConfirmed);
        Assert.Equal(buyerId, confirmed.RecipientUserId);
        Assert.Equal($"OrderConfirmed:{order.OrderId}", confirmed.EventKey);
        Assert.Contains("RT-ADM-01", confirmed.Content);

        var cancelled = Assert.Single(harness.Repo.Items, n => n.NotificationType == NotificationTypes.OrderCancelled);
        Assert.Equal(buyerId, cancelled.RecipientUserId);
        Assert.Equal($"OrderCancelled:{order.OrderId}", cancelled.EventKey);
        Assert.Contains("RT-ADM-01", cancelled.Content);
    }

    [Fact]
    public async Task AdminTrip_UpdateAndCancel_QueuesDeduplicatedNotificationsForBuyersAndLinkedParticipants()
    {
        var adminId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var companionId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var harness = new NotificationHarness(adminId, RoleNames.Admin);
        var trip = new Trip
        {
            TripId = tripId,
            TourId = Guid.NewGuid(),
            Tour = new Tour { Name = "Chư Yang Sin" },
            RefundPolicyId = Guid.NewGuid(),
            StartAt = Now.AddDays(10),
            EndAt = Now.AddDays(12),
            TotalSlots = 10,
            MinParticipants = 2,
            PricePerPerson = 2_000_000m,
            Status = TripStatuses.Open,
            RowVersion = 1
        };

        var bookings = new List<TripBookingOrder>();
        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_Notifications", _ => harness.Repo),
            ("get_Trips", _ => TestProxy.Create<ITripRepository>(
                ("GetTripForUpdateAsync", _ => Task.FromResult<Trip?>(trip)),
                ("GetSoldSlotsCountAsync", _ => Task.FromResult(0)),
                ("GetTripBookingsWithDetailsAsync", _ => Task.FromResult<IReadOnlyList<TripBookingOrder>>(bookings)),
                ("GetTripBookingsForUpdateAsync", _ => Task.FromResult<IReadOnlyList<TripBookingOrder>>(bookings)),
                ("GetSucceededPaymentsByOrderIdsAsync", _ => Task.FromResult<IReadOnlyList<Payment>>([])),
                ("GetActiveTripGroupsForUpdateAsync", _ => Task.FromResult<IReadOnlyList<TripGroup>>([])),
                ("GetTripProvidedItemsForUpdateAsync", _ => Task.FromResult<IReadOnlyList<TripProvidedItem>>([])),
                ("AddAuditLogAsync", _ => Task.CompletedTask))));

        var service = new AdminTripService(uow, harness.CurrentUser, harness.Clock, harness.Service, TestProxy.Create<IRentalRefundOpener>());

        // 1. Đổi lịch khi chưa có booking -> không tạo thông báo
        var shiftedStart = Now.AddDays(11);
        var shiftedEnd = Now.AddDays(13);
        await service.UpdateAsync(tripId, new UpdateAdminTripRequest
        {
            StartAt = shiftedStart,
            EndAt = shiftedEnd,
            TotalSlots = 10,
            MinParticipants = 2,
            PricePerPerson = 2_000_000m,
            RowVersion = 1
        });
        Assert.Empty(harness.Repo.Items);

        // Thêm 1 booking đã hủy (phải bỏ qua) và 1 booking hiệu lực (buyer đồng thời là participant + companion + guest chưa link)
        bookings.Add(new TripBookingOrder
        {
            OrderId = Guid.NewGuid(),
            TripId = tripId,
            Order = new Order { OrderId = Guid.NewGuid(), OrderCode = "RT-OLD", CustomerUserId = Guid.NewGuid(), Status = OrderStatuses.Cancelled },
            Participants = []
        });
        var activeOrderId = Guid.NewGuid();
        bookings.Add(new TripBookingOrder
        {
            OrderId = activeOrderId,
            TripId = tripId,
            Order = new Order { OrderId = activeOrderId, OrderCode = "RT-ACT-1", CustomerUserId = buyerId, Status = OrderStatuses.Confirmed },
            Participants =
            [
                new BookingParticipant { ParticipantId = Guid.NewGuid(), FullName = "Chủ đơn đi cùng", LinkedUserId = buyerId, Status = BookingParticipantStatuses.Pending },
                new BookingParticipant { ParticipantId = Guid.NewGuid(), FullName = "Bạn đi cùng", LinkedUserId = companionId, Status = BookingParticipantStatuses.Pending },
                new BookingParticipant { ParticipantId = Guid.NewGuid(), FullName = "Chưa có tài khoản", LinkedUserId = null, Status = BookingParticipantStatuses.Pending }
            ]
        });

        // 2. Cập nhật KHÔNG đổi StartAt/EndAt dù đã có booking -> không có thông báo TripScheduleChanged
        await service.UpdateAsync(tripId, new UpdateAdminTripRequest
        {
            StartAt = shiftedStart,
            EndAt = shiftedEnd,
            TotalSlots = 12,
            MinParticipants = 2,
            PricePerPerson = 2_000_000m,
            RowVersion = 2
        });
        Assert.Empty(harness.Repo.Items);

        // 3. Đổi lịch khi có booking hiệu lực -> đúng 2 thông báo (buyer không bị trùng, companion nhận 1)
        var newStart = Now.AddDays(14);
        var newEnd = Now.AddDays(16);
        await service.UpdateAsync(tripId, new UpdateAdminTripRequest
        {
            StartAt = newStart,
            EndAt = newEnd,
            TotalSlots = 12,
            MinParticipants = 2,
            PricePerPerson = 2_000_000m,
            RowVersion = 3
        });

        var expectedScheduleKey = $"TripScheduleChanged:{tripId}:{newStart.UtcDateTime:yyyyMMddHHmm}";
        var scheduleAlerts = harness.Repo.Items.Where(n => n.NotificationType == NotificationTypes.TripScheduleChanged).ToList();
        Assert.Equal(2, scheduleAlerts.Count);
        Assert.All(scheduleAlerts, n =>
        {
            Assert.Equal(expectedScheduleKey, n.EventKey);
            Assert.Contains("Chư Yang Sin", n.Content);
            Assert.Contains("RT-ACT-1", n.Content);
        });
        Assert.Contains(scheduleAlerts, n => n.RecipientUserId == buyerId);
        Assert.Contains(scheduleAlerts, n => n.RecipientUserId == companionId);

        // 4. Hủy chuyến -> đúng 2 thông báo TripCancelled cho buyer (chỉ 1) và companion
        await service.CancelTripAsync(tripId, new CancelAdminTripRequest { Reason = "Thời tiết xấu" });
        var cancelAlerts = harness.Repo.Items.Where(n => n.NotificationType == NotificationTypes.TripCancelled).ToList();
        Assert.Equal(2, cancelAlerts.Count);
        Assert.All(cancelAlerts, n =>
        {
            Assert.Equal($"TripCancelled:{tripId}", n.EventKey);
            Assert.Contains("Chư Yang Sin", n.Content);
            Assert.Contains("RT-ACT-1", n.Content);
        });
        Assert.Contains(cancelAlerts, n => n.RecipientUserId == buyerId);
        Assert.Contains(cancelAlerts, n => n.RecipientUserId == companionId);
    }

    [Theory]
    [InlineData(RefundCaseStatuses.Requested, "Reject")]
    [InlineData(RefundCaseStatuses.Approved, "Complete")]
    [InlineData(RefundCaseStatuses.Approved, "Fail")]
    public async Task RefundCase_TerminalTransitions_QueueRefundResultWithoutSensitiveAccountNumber(string initialStatus, string action)
    {
        var adminId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var refundCaseId = Guid.NewGuid();
        var harness = new NotificationHarness(adminId, RoleNames.Admin);
        var refund = new RefundCase
        {
            RefundCaseId = refundCaseId,
            OrderId = orderId,
            Order = new Order
            {
                OrderId = orderId,
                CustomerUserId = customerId,
                OrderCode = "RT-REF-99",
                PaymentStatus = OrderPaymentStatuses.Paid
            },
            CustomerUserId = customerId,
            Amount = 500_000m,
            Status = initialStatus,
            BankCode = "VCB",
            BankAccountLast4 = "6789",
            RefundPayment = new Payment { Status = PaymentStatuses.Pending },
            RowVersion = 1
        };

        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_Notifications", _ => harness.Repo),
            ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", _ => Task.CompletedTask))),
            ("get_Payments", _ => TestProxy.Create<IPaymentRepository>(
                ("AcquireOrderLockAsync", _ => Task.CompletedTask),
                ("ProviderTransactionExistsAsync", _ => Task.FromResult(false)))),
            ("get_TripBookings", _ => TestProxy.Create<ITripBookingRepository>(
                ("GetSucceededPaymentTotalAsync", _ => Task.FromResult(500_000m)))),
            ("get_RefundProofs", _ => TestProxy.Create<IRefundProofRepository>(("AddRangeAsync", _ => Task.CompletedTask))),
            ("get_RefundCases", _ => TestProxy.Create<IRefundCaseRepository>(
                ("GetForUpdateAsync", _ => Task.FromResult<RefundCase?>(refund)),
                ("ReloadOrderAsync", _ => Task.CompletedTask),
                ("ProviderTransactionUsedAsync", _ => Task.FromResult(false)),
                ("GetSucceededRefundTotalAsync", _ => Task.FromResult(0m)))));

        var service = new RefundCaseService(
            uow,
            harness.CurrentUser,
            harness.Clock,
            new RefundDataProtector(Microsoft.Extensions.Options.Options.Create(new RefundSettings
            {
                DataEncryptionKey = Convert.ToBase64String(new byte[32])
            })),
            TestProxy.Create<IRefundVerificationService>(),
            TestProxy.Create<IEmailSender>(),
            NullLogger<RefundCaseService>.Instance,
            harness.Service,
            privateStorage: ProofStorage.Create());

        if (action == "Reject")
            await service.RejectAsync(refundCaseId, new RefundDecisionRequest { Reason = "Sai thông tin", ExpectedRowVersion = 1 });
        else if (action == "Complete")
            await service.CompleteAsync(refundCaseId, new CompleteRefundRequest { ProviderTransactionId = "BANK-TX-1", ExpectedRowVersion = 1 }, ProofStorage.Files());
        else
            await service.MarkFailedAsync(refundCaseId, new RefundDecisionRequest { Reason = "Ngân hàng hoàn trả", ExpectedRowVersion = 1 });

        var notification = Assert.Single(harness.Repo.Items);
        Assert.Equal(customerId, notification.RecipientUserId);
        if (action == "Fail")
        {
            // Báo thất bại trả hồ sơ về cho khách nhập lại STK
            Assert.Equal(RefundCaseStatuses.AwaitingBankInfo, refund.Status);
            Assert.Equal(NotificationTypes.RefundBankInfoRequired, notification.NotificationType);
            Assert.Equal($"RefundBankInfoRequired:{refundCaseId}:{refund.RowVersion}", notification.EventKey);
        }
        else
        {
            Assert.Equal(NotificationTypes.RefundResult, notification.NotificationType);
            Assert.Equal($"RefundResult:{refundCaseId}:{refund.Status}", notification.EventKey);
        }
        Assert.Contains("RT-REF-99", notification.Content);
        Assert.DoesNotContain("6789", notification.Content);
        Assert.DoesNotContain("0123456789", notification.Content);
    }

    [AdminPostgresFact]
    public async Task Notifications_Postgres_UniqueEventKeyBlocksDuplicatesEnforcesUserIsolationAndIdempotentRead()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var context = database.Context();

        var clock = new TestClock { Current = Now };
        var currentUser = new TestCurrentUser { UserId = database.CustomerId, Role = RoleNames.Customer };
        var uow = new UnitOfWork(context);
        var service = new NotificationService(uow, currentUser, clock);

        var eventKey = $"TripCancelled:{database.TripId}";
        await service.QueueAsync(
            database.CustomerId, NotificationTypes.TripCancelled, "Hủy chuyến", "Chuyến đi đã bị hủy", "Trip", database.TripId, eventKey);
        // Gọi lần 2 trước khi SaveChangesAsync -> bỏ qua êm, không nổ khóa duy nhất
        await service.QueueAsync(
            database.CustomerId, NotificationTypes.TripCancelled, "Hủy chuyến", "Chuyến đi đã bị hủy", "Trip", database.TripId, eventKey);
        await uow.SaveChangesAsync();

        // Gọi lại lần 3 sau khi đã lưu DB -> vẫn bỏ qua
        await service.QueueAsync(
            database.CustomerId, NotificationTypes.TripCancelled, "Hủy chuyến", "Chuyến đi đã bị hủy", "Trip", database.TripId, eventKey);
        await uow.SaveChangesAsync();

        // Cùng eventKey nhưng khác recipient vẫn được phép lưu
        await service.QueueAsync(
            database.StaffId, NotificationTypes.TripCancelled, "Hủy chuyến", "Chuyến đi đã bị hủy", "Trip", database.TripId, eventKey);
        await uow.SaveChangesAsync();

        Assert.Equal(1, await context.Notifications.CountAsync(n => n.RecipientUserId == database.CustomerId && n.EventKey == eventKey));
        Assert.Equal(1, await context.Notifications.CountAsync(n => n.RecipientUserId == database.StaffId && n.EventKey == eventKey));

        // Cố tình chèn bản trùng trực tiếp qua EF để xác minh partial unique index ux_notifications_recipient_event_key trên Postgres
        context.Notifications.Add(new Notification
        {
            NotificationId = Guid.NewGuid(),
            RecipientUserId = database.CustomerId,
            NotificationType = NotificationTypes.TripCancelled,
            Title = "Trùng",
            Content = "Trùng event_key",
            EventKey = eventKey,
            CreatedAt = Now.UtcDateTime
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
        Assert.Equal("ux_notifications_recipient_event_key", pg.ConstraintName);
        context.ChangeTracker.Clear();

        // Cô lập dữ liệu giữa 2 user + đọc 1 giữ nguyên ReadAt + read-all chỉ đánh dấu của mình
        var listCustomer = await service.GetMyNotificationsAsync(null);
        var notificationId = Assert.Single(listCustomer.Items).NotificationId;

        currentUser.UserId = database.StaffId;
        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkAsReadAsync(notificationId));

        currentUser.UserId = database.CustomerId;
        var firstRead = await service.MarkAsReadAsync(notificationId);
        clock.Current = Now.AddMinutes(10);
        var secondRead = await service.MarkAsReadAsync(notificationId);
        Assert.Equal(firstRead.ReadAt, secondRead.ReadAt);

        var markedAll = await service.MarkAllAsReadAsync();
        Assert.Equal(0, markedAll.MarkedCount);
        currentUser.UserId = database.StaffId;
        Assert.Equal(1, (await service.GetUnreadCountAsync()).UnreadCount);
        Assert.Equal(1, (await service.MarkAllAsReadAsync()).MarkedCount);
    }

    private sealed class NotificationHarness
    {
        public NotificationHarness(Guid initialUserId, string role = RoleNames.Customer)
        {
            Repo = new InMemoryNotificationRepository();
            Clock = new TestClock { Current = Now };
            CurrentUser = new TestCurrentUser { UserId = initialUserId, Role = role };
            var uow = TestProxy.Create<IUnitOfWork>(
                ("get_Notifications", _ => Repo),
                ("SaveChangesAsync", _ => { SaveChangesCalls++; return Task.FromResult(1); }
            ));
            Service = new NotificationService(uow, CurrentUser, Clock);
        }

        public InMemoryNotificationRepository Repo { get; }
        public TestClock Clock { get; }
        public TestCurrentUser CurrentUser { get; }
        public NotificationService Service { get; }
        public int SaveChangesCalls { get; private set; }

        public void ActAs(Guid userId) => CurrentUser.UserId = userId;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public Guid RequiredUserId => UserId ?? throw new UnauthorizedException("Chưa đăng nhập");
        public string? Jti => null;
        public string? Role { get; set; } = RoleNames.Customer;
        public bool IsAuthenticated => UserId is not null;
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<Notification> Items { get; } = [];

        public Task AddAsync(Notification entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByEventKeyAsync(Guid recipientUserId, string eventKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(n => n.RecipientUserId == recipientUserId && n.EventKey == eventKey));

        public Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedForUserAsync(
            Guid recipientUserId, bool? unreadOnly, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = Items.Where(n => n.RecipientUserId == recipientUserId && (unreadOnly != true || n.ReadAt is null));
            var ordered = query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyList<Notification>, int)>((ordered, query.Count()));
        }

        public Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Count(n => n.RecipientUserId == recipientUserId && n.ReadAt is null));

        public Task<Notification?> GetOwnedForUpdateAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.SingleOrDefault(n => n.NotificationId == notificationId && n.RecipientUserId == recipientUserId));

        public Task<IReadOnlyList<Notification>> GetUnreadOwnedForUpdateAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Notification>>(Items.Where(n => n.RecipientUserId == recipientUserId && n.ReadAt is null).ToList());

        public Task<Notification?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Notification> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(Notification entity) { }
        public void Remove(Notification entity) => Items.Remove(entity);
        public void RemoveRange(IEnumerable<Notification> entities) => throw new NotSupportedException();
    }
}
