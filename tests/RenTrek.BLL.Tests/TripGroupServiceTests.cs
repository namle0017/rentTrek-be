using System.Globalization;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripGroup.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Bộ unit test bao phủ đầy đủ 21 yêu cầu bắt buộc tại mục 7.6 của tài liệu thiết kế Chia TripGroup
/// cùng toàn bộ contract snapshot, rowVersion và ErrorCodes tại 03-API-CONTRACT.md §8.
/// </summary>
public sealed class TripGroupServiceTests
{
    private static readonly DateTimeOffset BaseNow = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    // 7.6 #1: Tạo Trip sinh đúng groupCount; group lấy đúng GroupMaxParticipants, ở Draft và chưa có guide.
    // Đồng thời chặn tạo group nếu Tour cũ chưa cấu hình GroupMaxParticipants.
    [Fact]
    public async Task CreateTripAsync_CreatesExactGroupCountWithTourMaxParticipantsDraftAndNoGuide()
    {
        var store = new SharedStore();
        var unconfiguredTour = store.SeedTour(groupMaxParticipants: null);
        var configuredTour = store.SeedTour(groupMaxParticipants: 8);
        var scope = store.CreateScope(store.AdminUser);

        var unconfiguredEx = await Assert.ThrowsAsync<ConflictException>(() => scope.Trips.CreateTripAsync(new CreateTripRequest
        {
            TourId = unconfiguredTour.Id,
            StartAt = BaseNow.AddDays(10),
            EndAt = BaseNow.AddDays(12),
            TotalSlots = 16,
            GroupCount = 2,
            PricePerPerson = 2_000_000m
        }));
        Assert.Equal(ErrorCodes.TripOperationalCapacityInsufficient, unconfiguredEx.ErrorCode);

        var created = await scope.Trips.CreateTripAsync(new CreateTripRequest
        {
            TourId = configuredTour.Id,
            StartAt = BaseNow.AddDays(10),
            EndAt = BaseNow.AddDays(12),
            TotalSlots = 15,
            GroupCount = 2,
            PricePerPerson = 2_000_000m,
            MinParticipants = 6
        });

        Assert.Equal(TripStatuses.Draft, created.Status);
        Assert.Equal(15, created.TotalSlots); // TotalSlots giữ nguyên, không tính lại từ số group
        Assert.Equal(2, created.GroupCount);
        Assert.Equal(2, created.Groups.Count);
        Assert.Equal(["Nhóm 1", "Nhóm 2"], created.Groups.Select(g => g.GroupName).ToArray());
        Assert.All(created.Groups, g =>
        {
            Assert.Equal(8, g.MaxParticipants);
            Assert.Equal(TripGroupStatuses.Draft, g.Status);
            Assert.Null(g.GuideUserId);
        });
    }

    // 7.6 #2: Lịch lặp sinh Trip và đúng số group từ DefaultGroupCount; chạy lại cùng occurrence không tạo trùng Trip hoặc group.
    [Fact]
    public async Task GenerateRecurringTripsAsync_GeneratesTripsWithDefaultGroupCount_AndIsIdempotentOnRerun()
    {
        var store = new SharedStore();
        var tour = store.SeedTour(groupMaxParticipants: 6);
        var scope = store.CreateScope(store.AdminUser);

        var rule = await scope.Recurrence.CreateRecurrenceRuleAsync(tour.Id, new CreateTourRecurrenceRuleRequest
        {
            Frequency = "Weekly",
            IntervalValue = 1,
            StartDate = new DateOnly(2026, 11, 7),
            EndDate = new DateOnly(2026, 11, 14),
            StartTime = new TimeOnly(6, 0),
            DaysOfWeek = "Sat",
            DefaultTotalSlots = 12,
            DefaultGroupCount = 2,
            DefaultPrice = 2_200_000m
        });

        Assert.Equal(2, rule.DefaultGroupCount);

        var firstRun = await scope.Recurrence.GenerateRecurringTripsAsync(new GenerateRecurringTripsRequest
        {
            TourId = tour.Id
        });

        Assert.Equal(2, firstRun.GeneratedTripsCount);
        Assert.Equal(4, firstRun.GeneratedGroupsCount);
        Assert.Equal(0, firstRun.SkippedExistingCount);
        Assert.Empty(firstRun.Warnings);
        Assert.All(firstRun.GeneratedTrips, t =>
        {
            Assert.Equal(TripStatuses.Draft, t.Status);
            Assert.Equal(12, t.TotalSlots);
            Assert.Equal(2, t.GroupCount);
            Assert.All(t.Groups, g =>
            {
                Assert.Equal(6, g.MaxParticipants);
                Assert.Equal(TripGroupStatuses.Draft, g.Status);
                Assert.Null(g.GuideUserId);
            });
        });

        // Chạy lại cùng occurrence: không tạo trùng Trip hoặc group
        var secondRun = await scope.Recurrence.GenerateRecurringTripsAsync(new GenerateRecurringTripsRequest
        {
            TourId = tour.Id
        });

        Assert.Equal(0, secondRun.GeneratedTripsCount);
        Assert.Equal(0, secondRun.GeneratedGroupsCount);
        Assert.Equal(2, secondRun.SkippedExistingCount);
        Assert.Equal(2, store.Trips.Count);
        Assert.Equal(4, store.Groups.Count);
    }

    // 7.6 #3: Rule lịch lặp cũ thiếu DefaultGroupCount không sinh Trip và tạo cảnh báo vận hành.
    [Fact]
    public async Task GenerateRecurringTripsAsync_LegacyRuleMissingDefaultGroupCount_SkipsTripAndCreatesOperationalWarning()
    {
        var store = new SharedStore();
        var tour = store.SeedTour(groupMaxParticipants: 8);
        var legacyRule = new TourRecurrenceRule
        {
            RecurrenceRuleId = Guid.NewGuid(),
            TourId = tour.Id,
            Tour = tour,
            Frequency = "Weekly",
            IntervalValue = 1,
            StartDate = new DateOnly(2026, 11, 7),
            EndDate = new DateOnly(2026, 11, 21),
            StartTime = new TimeOnly(6, 0),
            DaysOfWeek = "Sat",
            DefaultTotalSlots = 16,
            DefaultGroupCount = null,
            DefaultPrice = 2_000_000m,
            IsActive = true,
            CreatedAt = BaseNow.UtcDateTime,
            UpdatedAt = BaseNow.UtcDateTime
        };
        store.RecurrenceRules.Add(legacyRule);

        var scope = store.CreateScope(store.AdminUser);
        var result = await scope.Recurrence.GenerateRecurringTripsAsync(new GenerateRecurringTripsRequest
        {
            TourId = tour.Id
        });

        Assert.Equal(0, result.GeneratedTripsCount);
        Assert.Empty(store.Trips);
        Assert.Empty(store.Groups);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(legacyRule.RecurrenceRuleId, warning.RecurrenceRuleId);
        Assert.Equal("RECURRENCE_MISSING_DEFAULT_GROUP_COUNT", warning.Code);
        Assert.Contains(store.AuditLogs, a => a.Action == "RecurrenceRuleMissingGroupConfig" && a.EntityId == legacyRule.RecurrenceRuleId);
        Assert.Contains(store.Notifications, n => n.RelatedEntityId == legacyRule.RecurrenceRuleId && n.NotificationType == "OperationalWarning");
    }

    [Fact]
    public async Task UpdateTripStatusAsync_RejectsOpeningWhenNoRefundPolicyResolves()
    {
        var store = new SharedStore();
        var tour = store.SeedTour(groupMaxParticipants: 6);
        var scope = store.CreateScope(store.AdminUser);
        var draft = await scope.Trips.CreateTripAsync(new CreateTripRequest
        {
            TourId = tour.Id,
            StartAt = BaseNow.AddDays(14),
            EndAt = BaseNow.AddDays(16),
            TotalSlots = 6,
            GroupCount = 1
        });
        store.Trips.Single().RefundPolicyId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<ConflictException>(() => scope.Trips.UpdateTripStatusAsync(
            draft.TripId, new UpdateTripStatusRequest { Status = TripStatuses.Open }));

        Assert.Equal(ErrorCodes.RefundPolicyChanged, error.ErrorCode);
        Assert.Equal(TripStatuses.Draft, store.Trips.Single().Status);
    }

    // 7.6 #4 & #5: Trip Draft được lưu khi sức chứa group chưa đủ; chuyển Open bị từ chối cho tới khi sức chứa gồm overflow đủ TotalSlots.
    // Không tạo hoặc xóa group sau khi Trip Open.
    [Fact]
    public async Task OpenTripAndPostOpenImmutability_EnforcesCapacityWithOverflow_AndBlocksGroupAddRemoveAfterOpen()
    {
        var store = new SharedStore { OverflowSlots = 2 };
        var tour = store.SeedTour(groupMaxParticipants: 6);
        var scope = store.CreateScope(store.AdminUser);

        // 1 group: Max = 6, Overflow = 2 => sức chứa vận hành = 8 < TotalSlots = 15 => vẫn lưu Draft thành công
        var draft = await scope.Trips.CreateTripAsync(new CreateTripRequest
        {
            TourId = tour.Id,
            StartAt = BaseNow.AddDays(14),
            EndAt = BaseNow.AddDays(16),
            TotalSlots = 15,
            GroupCount = 1,
            PricePerPerson = 1_800_000m
        });

        Assert.Equal(TripStatuses.Draft, draft.Status);
        Assert.False(draft.CanOpenForBooking);
        Assert.NotNull(draft.OpenReadinessWarning);

        // Chuyển sang Open bị từ chối vì 8 < 15
        var openEx = await Assert.ThrowsAsync<ConflictException>(() => scope.Trips.UpdateTripStatusAsync(
            draft.TripId,
            new UpdateTripStatusRequest { Status = TripStatuses.Open }));
        Assert.Equal(ErrorCodes.TripOperationalCapacityInsufficient, openEx.ErrorCode);

        // Khi còn Draft, Admin nâng lên 2 group => (6 + 2) * 2 = 16 >= 15 (sức chứa chuẩn 12 < 15 nhưng gồm overflow 16 >= 15)
        var updatedDraft = await scope.Trips.UpdateTripAsync(draft.TripId, new UpdateTripRequest
        {
            StartAt = BaseNow.AddDays(14),
            EndAt = BaseNow.AddDays(16),
            TotalSlots = 15,
            GroupCount = 2,
            PricePerPerson = 1_800_000m,
            MinParticipants = 1
        });
        Assert.True(updatedDraft.CanOpenForBooking);
        Assert.Equal(2, updatedDraft.GroupCount);

        // Nay chuyển sang Open thành công
        var opened = await scope.Trips.UpdateTripStatusAsync(
            draft.TripId,
            new UpdateTripStatusRequest { Status = TripStatuses.Open });
        Assert.Equal(TripStatuses.Open, opened.Status);

        // Sau khi Open: cả thêm group (3) lẫn xóa group (1) đều bị từ chối với mã TRIP_GROUP_COUNT_LOCKED
        var addAfterOpenEx = await Assert.ThrowsAsync<ConflictException>(() => scope.Trips.UpdateTripAsync(draft.TripId, new UpdateTripRequest
        {
            StartAt = BaseNow.AddDays(14),
            EndAt = BaseNow.AddDays(16),
            TotalSlots = 15,
            GroupCount = 3,
            PricePerPerson = 1_800_000m,
            MinParticipants = 1
        }));
        Assert.Equal(ErrorCodes.TripGroupCountLocked, addAfterOpenEx.ErrorCode);

        var removeAfterOpenEx = await Assert.ThrowsAsync<ConflictException>(() => scope.Trips.UpdateTripAsync(draft.TripId, new UpdateTripRequest
        {
            StartAt = BaseNow.AddDays(14),
            EndAt = BaseNow.AddDays(16),
            TotalSlots = 15,
            GroupCount = 1,
            PricePerPerson = 1_800_000m,
            MinParticipants = 1
        }));
        Assert.Equal(ErrorCodes.TripGroupCountLocked, removeAfterOpenEx.ErrorCode);
    }

    // 7.6 #6: Booking chưa Paid không được xếp; một trạng thái đơn lẻ không kích hoạt tự xếp; callback hợp lệ xếp đúng một lần.
    [Fact]
    public async Task AutoAssign_RequiresBothConfirmedAndPaid_AndIsIdempotent()
    {
        var store = new SharedStore();
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 6, groupCount: 2, totalSlots: 12);
        var scope = store.CreateScope(store.StaffUser);

        var pendingOrder = store.SeedBooking(trip, store.BuyerUser, seatCount: 2, status: "PendingPayment", paymentStatus: "Unpaid");
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(pendingOrder.OrderId);
        Assert.All(pendingOrder.TripBookingOrder.Participants, p => Assert.Null(p.TripGroupId));

        pendingOrder.Status = "Confirmed";
        pendingOrder.PaymentStatus = "Unpaid";
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(pendingOrder.OrderId);
        Assert.All(pendingOrder.TripBookingOrder.Participants, p => Assert.Null(p.TripGroupId));

        pendingOrder.Status = "PendingPayment";
        pendingOrder.PaymentStatus = "Paid";
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(pendingOrder.OrderId);
        Assert.All(pendingOrder.TripBookingOrder.Participants, p => Assert.Null(p.TripGroupId));

        pendingOrder.Status = "Confirmed";
        pendingOrder.PaymentStatus = "Paid";
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(pendingOrder.OrderId);

        var assignedGroupId = pendingOrder.TripBookingOrder.Participants.First().TripGroupId;
        Assert.NotNull(assignedGroupId);
        Assert.All(pendingOrder.TripBookingOrder.Participants, p => Assert.Equal(assignedGroupId, p.TripGroupId));

        var auditCountBeforeDuplicate = store.AuditLogs.Count(a => a.Action == "AutoAssignBookingToGroup");
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(pendingOrder.OrderId);
        var auditCountAfterDuplicate = store.AuditLogs.Count(a => a.Action == "AutoAssignBookingToGroup");

        Assert.Equal(auditCountBeforeDuplicate, auditCountAfterDuplicate);
        Assert.All(pendingOrder.TripBookingOrder.Participants, p => Assert.Equal(assignedGroupId, p.TripGroupId));
    }

    // 7.6 #7 & #8: Booking Paid vào group ít người nhất còn chứa trọn booking; điều kiện hòa ổn định (CreatedAt rồi TripGroupId).
    // Booking không vừa sức chứa chuẩn nằm ở Chưa xếp, không tự dùng overflow và không tự tách.
    [Fact]
    public async Task AutoAssign_BalancesBySmallestHeadcountWithDeterministicTieBreaker_AndQueuesOversizedToUnassignedWithoutOverflowOrSplit()
    {
        var store = new SharedStore { OverflowSlots = 2 };
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 5, groupCount: 2, totalSlots: 14);
        var groups = trip.TripGroups.OrderBy(g => g.CreatedAt).ThenBy(g => g.TripGroupId).ToList();
        var group1 = groups[0];
        var group2 = groups[1];
        var sameCreated = BaseNow.UtcDateTime;
        group1.CreatedAt = sameCreated;
        group2.CreatedAt = sameCreated;
        var expectedFirstTieGroup = new[] { group1, group2 }.OrderBy(g => g.TripGroupId).First();
        var otherGroup = expectedFirstTieGroup.TripGroupId == group1.TripGroupId ? group2 : group1;

        var scope = store.CreateScope(store.StaffUser);

        var booking1 = store.SeedBooking(trip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(booking1.OrderId);
        Assert.All(booking1.TripBookingOrder.Participants, p => Assert.Equal(expectedFirstTieGroup.TripGroupId, p.TripGroupId));

        var booking2 = store.SeedBooking(trip, store.SecondBuyerUser, seatCount: 2, status: "Confirmed", paymentStatus: "Paid");
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(booking2.OrderId);
        Assert.All(booking2.TripBookingOrder.Participants, p => Assert.Equal(otherGroup.TripGroupId, p.TripGroupId));

        // Booking 3 (4 người): vừa nếu dùng overflow của otherGroup (2 + 4 = 6 <= 5 + 2), NHƯNG không vừa sức chứa chuẩn (6 > 5)!
        var booking3 = store.SeedBooking(trip, store.BuyerUser, seatCount: 4, status: "Confirmed", paymentStatus: "Paid");
        await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(booking3.OrderId);

        Assert.All(booking3.TripBookingOrder.Participants, p => Assert.Null(p.TripGroupId));
        Assert.False(booking3.TripBookingOrder.IsSplit);

        var snapshot = await scope.Grouping.GetGroupingSnapshotAsync(trip.TripId);
        var unassigned = Assert.Single(snapshot.UnassignedBookings);
        Assert.Equal(booking3.OrderId, unassigned.BookingOrderId);
        Assert.Equal(4, unassigned.ParticipantCount);
        Assert.Equal(4, snapshot.UnassignedParticipants);
        Assert.Equal(5, snapshot.AssignedParticipants);
    }

    // 7.6 #9, #10, #11: Hủy booking không làm cân bằng lại các booking khác; booking mới dùng số lượng hiện tại.
    // Giữ TripGroupId cho lịch sử nhưng giải phóng sức chứa; participant đơn hủy không xuất hiện ở manifest Staff, màn Guide, màn Khách.
    [Fact]
    public async Task CancelBooking_RetainsHistoryGroup_ReleasesCapacityWithoutRebalancing_AndHidesFromStaffGuideAndCustomerViews()
    {
        var store = new SharedStore();
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 4, groupCount: 2, totalSlots: 10);
        var groups = trip.TripGroups.OrderBy(g => g.CreatedAt).ThenBy(g => g.TripGroupId).ToList();
        var group1 = groups[0];
        var group2 = groups[1];

        var staffScope = store.CreateScope(store.StaffUser);

        var bookingA = store.SeedBooking(trip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(bookingA.OrderId);
        var bookingB = store.SeedBooking(trip, store.SecondBuyerUser, seatCount: 2, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(bookingB.OrderId);
        var bookingC = store.SeedBooking(trip, store.SecondBuyerUser, seatCount: 1, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(bookingC.OrderId);

        Assert.All(bookingA.TripBookingOrder.Participants, p => Assert.Equal(group1.TripGroupId, p.TripGroupId));
        Assert.All(bookingB.TripBookingOrder.Participants, p => Assert.Equal(group2.TripGroupId, p.TripGroupId));
        Assert.All(bookingC.TripBookingOrder.Participants, p => Assert.Equal(group2.TripGroupId, p.TripGroupId));

        await staffScope.Grouping.AssignGuideAsync(trip.TripId, group1.TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide1User.UserId
        });

        var buyerAScope = store.CreateScope(store.BuyerUser);
        var cancelResult = await buyerAScope.TripBookings.CancelAsync(bookingA.OrderId, new CancelTripBookingRequest
        {
            Reason = "Đổi kế hoạch"
        });

        Assert.Equal(OrderStatuses.Cancelled, cancelResult.Status);
        Assert.Empty(cancelResult.Participants);

        // 1) TripGroupId vẫn giữ nguyên để truy vết lịch sử + có AuditLog
        Assert.All(bookingA.TripBookingOrder.Participants, p =>
        {
            Assert.Equal(group1.TripGroupId, p.TripGroupId);
            Assert.Equal("Cancelled", p.Status);
        });
        Assert.Contains(store.AuditLogs, a => a.Action == "CancelTripBooking" && a.EntityId == bookingA.OrderId);

        // 2) Không di chuyển bookingB / bookingC ở group2 sang group1
        Assert.All(bookingB.TripBookingOrder.Participants, p => Assert.Equal(group2.TripGroupId, p.TripGroupId));
        Assert.All(bookingC.TripBookingOrder.Participants, p => Assert.Equal(group2.TripGroupId, p.TripGroupId));

        // 3) Manifest Staff: group1 hiện 0 người tham gia hiệu lực
        var snapshotAfterCancel = await staffScope.Grouping.GetGroupingSnapshotAsync(trip.TripId);
        var staffGroup1 = snapshotAfterCancel.Groups.Single(g => g.TripGroupId == group1.TripGroupId);
        Assert.Equal(0, staffGroup1.ParticipantCount);
        Assert.Empty(staffGroup1.Bookings);
        Assert.True(snapshotAfterCancel.ProvidedItemsNeedsRecalc);

        // 4) Màn Guide (cả /grouping lẫn /guide/trip-groups): group1 rỗng, không lộ participant đơn hủy
        var guideScope = store.CreateScope(store.Guide1User);
        var guideSnapshot = await guideScope.Grouping.GetGroupingSnapshotAsync(trip.TripId);
        var guideGroupCard = Assert.Single(guideSnapshot.Groups);
        Assert.Equal(0, guideGroupCard.ParticipantCount);
        Assert.Empty(guideGroupCard.Bookings);
        var guideGroup1 = await guideScope.Views.GetGuideGroupByIdAsync(group1.TripGroupId);
        Assert.Equal(0, guideGroup1.CurrentParticipants);
        Assert.Empty(guideGroup1.Participants);

        // 5) Booking mới D (4 người) dùng số lượng hiện tại của group1 (0/4) nên xếp vừa trọn vào group1
        var bookingD = store.SeedBooking(trip, store.SecondBuyerUser, seatCount: 4, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(bookingD.OrderId);
        Assert.All(bookingD.TripBookingOrder.Participants, p => Assert.Equal(group1.TripGroupId, p.TripGroupId));

        // 6) Kể cả sau khi Published, màn khách của bookingA vẫn không trả participant đã hủy hay của bookingD
        await staffScope.Grouping.AssignGuideAsync(trip.TripId, group2.TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide2User.UserId
        });
        await staffScope.Trips.UpdateTripStatusAsync(trip.TripId, new UpdateTripStatusRequest
        {
            Status = TripStatuses.ClosedForBooking
        });
        await staffScope.Publish.RecalculateProvidedItemsAsync(trip.TripId);
        await staffScope.Publish.PublishGroupsAsync(trip.TripId);

        var cancelledCustomerGrouping = await buyerAScope.Views.GetCustomerGroupingAsync(bookingA.OrderId);
        Assert.Empty(cancelledCustomerGrouping.Groups);
    }

    // 7.6 #12: Hai booking thanh toán đồng thời không làm group vượt MaxParticipants.
    [Fact]
    public async Task ConcurrentAutoAssign_WithPerTripLock_NeverExceedsMaxParticipants()
    {
        var store = new SharedStore { OverflowSlots = 2, SimulateConcurrentReadDelay = true };
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 4, groupCount: 1, totalSlots: 6);
        var onlyGroup = Assert.Single(trip.TripGroups);

        // Hai booking đều 3 người cùng thanh toán đồng thời vào 1 group sức chứa chuẩn = 4 (3 + 3 = 6 > 4)
        var booking1 = store.SeedBooking(trip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        var booking2 = store.SeedBooking(trip, store.SecondBuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");

        async Task RunWebhookTransactionAsync(Guid orderId)
        {
            var scope = store.CreateScope(store.StaffUser);
            await scope.UnitOfWork.BeginTransactionAsync();
            try
            {
                await scope.Grouping.AutoAssignPaidBookingInTransactionAsync(orderId);
                await scope.UnitOfWork.SaveChangesAsync();
                await scope.UnitOfWork.CommitAsync();
            }
            catch
            {
                await scope.UnitOfWork.RollbackAsync();
                throw;
            }
        }

        await Task.WhenAll(
            RunWebhookTransactionAsync(booking1.OrderId),
            RunWebhookTransactionAsync(booking2.OrderId));

        var verifyScope = store.CreateScope(store.StaffUser);
        var snapshot = await verifyScope.Grouping.GetGroupingSnapshotAsync(trip.TripId);
        var groupState = Assert.Single(snapshot.Groups);

        Assert.Equal(3, groupState.ParticipantCount);
        Assert.True(groupState.ParticipantCount <= onlyGroup.MaxParticipants);
        var unassigned = Assert.Single(snapshot.UnassignedBookings);
        Assert.Equal(3, unassigned.ParticipantCount);
    }

    // 7.6 #13 & #14 + CONCURRENCY_CONFLICT & GROUP_WRONG_TRIP:
    // Staff xếp tay trong overflow thành công; vượt MaxParticipants + GroupOverflowSlots bị từ chối.
    // Tách booking thiếu lý do hoặc audit metadata bị từ chối; đủ dữ liệu thì ghi IsSplit, SplitReason, SplitApprovedByUserId, SplitAt và AuditLog.
    [Fact]
    public async Task StaffManualOverflowAndSplit_EnforcesOverflowCeiling_RowVersion_AndRequiresSplitReasonAndApproverMetadata()
    {
        var store = new SharedStore { OverflowSlots = 2 };
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 4, groupCount: 2, totalSlots: 12);
        var groups = trip.TripGroups.OrderBy(g => g.CreatedAt).ThenBy(g => g.TripGroupId).ToList();
        var group1 = groups[0];
        var group2 = groups[1];
        var staffScope = store.CreateScope(store.StaffUser);

        var b1 = store.SeedBooking(trip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(b1.OrderId);
        var b2 = store.SeedBooking(trip, store.SecondBuyerUser, seatCount: 4, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(b2.OrderId);

        var b3 = store.SeedBooking(trip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(b3.OrderId);
        Assert.All(b3.TripBookingOrder.Participants, p => Assert.Null(p.TripGroupId));

        // expectedTripRowVersion cũ bị từ chối với CONCURRENCY_CONFLICT
        var staleEx = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Grouping.MoveBookingAsync(
            trip.TripId,
            b3.OrderId,
            new MoveBookingGroupRequest
            {
                TargetGroupId = group1.TripGroupId,
                ExpectedTripRowVersion = 999
            }));
        Assert.Equal(ErrorCodes.ConcurrencyConflict, staleEx.ErrorCode);

        // Group không thuộc Trip bị từ chối với GROUP_WRONG_TRIP (400)
        var wrongTripEx = await Assert.ThrowsAsync<BadRequestException>(() => staffScope.Grouping.MoveBookingAsync(
            trip.TripId,
            b3.OrderId,
            new MoveBookingGroupRequest
            {
                TargetGroupId = Guid.NewGuid()
            }));
        Assert.Equal(ErrorCodes.GroupWrongTrip, wrongTripEx.ErrorCode);

        // 1) Staff chuyển nguyên b3 (3 người) vào group1 (đang 3 người): 3 + 3 = 6 <= 4 + 2 (overflow) => THÀNH CÔNG
        var afterMove = await staffScope.Grouping.MoveBookingAsync(trip.TripId, b3.OrderId, new MoveBookingGroupRequest
        {
            TargetGroupId = group1.TripGroupId,
            ExpectedTripRowVersion = trip.RowVersion,
            Note = "Xếp dùng 2 chỗ overflow"
        });
        var movedGroup1 = afterMove.Groups.Single(g => g.TripGroupId == group1.TripGroupId);
        Assert.Equal(6, movedGroup1.ParticipantCount);
        Assert.True(movedGroup1.IsOverflowing);
        Assert.Equal(0, movedGroup1.RemainingStandardSlots);
        Assert.Equal(0, movedGroup1.RemainingOverflowSlots);

        // 2) Chuyển b3 (3 người) sang group2 (4 + 3 = 7 > 6) => TỪ CHỐI với GROUP_CAPACITY_EXCEEDED
        var capEx = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Grouping.MoveBookingAsync(
            trip.TripId,
            b3.OrderId,
            new MoveBookingGroupRequest
            {
                TargetGroupId = group2.TripGroupId
            }));
        Assert.Equal(ErrorCodes.GroupCapacityExceeded, capEx.ErrorCode);

        // 3) Kiểm tra tách booking
        var splitTrip = await CreateOpenTripAsync(store, groupMaxParticipants: 4, groupCount: 2, totalSlots: 12);
        var sg = splitTrip.TripGroups.OrderBy(g => g.CreatedAt).ThenBy(g => g.TripGroupId).ToList();
        var fill1 = store.SeedBooking(splitTrip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        var fill2 = store.SeedBooking(splitTrip, store.SecondBuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(fill1.OrderId);
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(fill2.OrderId);

        var toSplit = store.SeedBooking(splitTrip, store.BuyerUser, seatCount: 4, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(toSplit.OrderId);
        var seats = toSplit.TripBookingOrder.Participants.ToList();
        var validAssignments = new List<ParticipantGroupAssignmentRequest>
        {
            new() { TripGroupId = sg[0].TripGroupId, ParticipantIds = [seats[0].ParticipantId, seats[1].ParticipantId] },
            new() { TripGroupId = sg[1].TripGroupId, ParticipantIds = [seats[2].ParticipantId, seats[3].ParticipantId] }
        };

        // Thiếu lý do tách => bị từ chối
        await Assert.ThrowsAsync<BadRequestException>(() => staffScope.Grouping.SplitBookingAsync(
            splitTrip.TripId,
            toSplit.OrderId,
            new SplitTripBookingRequest
            {
                Reason = "   ",
                Assignments = validAssignments
            }));

        // Thiếu người duyệt (audit metadata UserId rỗng / không hợp lệ) => bị từ chối
        var anonymousScope = store.CreateScope(account: null);
        await Assert.ThrowsAsync<BadRequestException>(() => anonymousScope.Grouping.SplitBookingAsync(
            splitTrip.TripId,
            toSplit.OrderId,
            new SplitTripBookingRequest
            {
                Reason = "Khách đồng ý tách 2-2 do hết chỗ nguyên đoàn",
                Assignments = validAssignments
            }));

        // Đủ lý do và metadata người duyệt => thành công, ghi đầy đủ trường tách và AuditLog
        var splitSnapshot = await staffScope.Grouping.SplitBookingAsync(
            splitTrip.TripId,
            toSplit.OrderId,
            new SplitTripBookingRequest
            {
                Reason = "Khách đồng ý tách 2-2 do hết chỗ nguyên đoàn",
                ExpectedTripRowVersion = splitTrip.RowVersion,
                Assignments = validAssignments
            });

        Assert.True(toSplit.TripBookingOrder.IsSplit);
        Assert.Equal("Khách đồng ý tách 2-2 do hết chỗ nguyên đoàn", toSplit.TripBookingOrder.SplitReason);
        Assert.Equal(store.StaffUser.UserId, toSplit.TripBookingOrder.SplitApprovedByUserId);
        Assert.Equal(BaseNow, toSplit.TripBookingOrder.SplitAt);
        Assert.Empty(splitSnapshot.UnassignedBookings);
        Assert.Contains(store.AuditLogs, a => a.Action == "SplitBookingAcrossGroups" && a.EntityId == toSplit.OrderId && a.ActorUserId == store.StaffUser.UserId);
    }

    // 7.6 #15: Gán một guide cho hai group cùng Trip hoặc hai Trip giao thời gian bị từ chối.
    [Fact]
    public async Task AssignGuideAsync_RejectsSameGuideInTwoGroupsOfSameTrip_OrOverlappingOtherTrip()
    {
        var store = new SharedStore();
        var tripA = await CreateOpenTripAsync(
            store,
            groupMaxParticipants: 8,
            groupCount: 2,
            totalSlots: 16,
            startAt: BaseNow.AddDays(10),
            endAt: BaseNow.AddDays(12));
        var tripBOverlapping = await CreateOpenTripAsync(
            store,
            groupMaxParticipants: 8,
            groupCount: 1,
            totalSlots: 8,
            startAt: BaseNow.AddDays(11),
            endAt: BaseNow.AddDays(13));
        var tripCNonOverlapping = await CreateOpenTripAsync(
            store,
            groupMaxParticipants: 8,
            groupCount: 1,
            totalSlots: 8,
            startAt: BaseNow.AddDays(12),
            endAt: BaseNow.AddDays(14));

        var groupsA = tripA.TripGroups.ToList();
        var groupB = tripBOverlapping.TripGroups.Single();
        var groupC = tripCNonOverlapping.TripGroups.Single();
        var staffScope = store.CreateScope(store.StaffUser);

        await staffScope.Grouping.AssignGuideAsync(tripA.TripId, groupsA[0].TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide1User.UserId
        });

        var sameTripEx = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Grouping.AssignGuideAsync(
            tripA.TripId,
            groupsA[1].TripGroupId,
            new AssignTripGroupGuideRequest { GuideUserId = store.Guide1User.UserId }));
        Assert.Equal(ErrorCodes.GuideScheduleConflict, sameTripEx.ErrorCode);

        var overlapEx = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Grouping.AssignGuideAsync(
            tripBOverlapping.TripId,
            groupB.TripGroupId,
            new AssignTripGroupGuideRequest { GuideUserId = store.Guide1User.UserId }));
        Assert.Equal(ErrorCodes.GuideScheduleConflict, overlapEx.ErrorCode);

        var nonOverlapResult = await staffScope.Grouping.AssignGuideAsync(
            tripCNonOverlapping.TripId,
            groupC.TripGroupId,
            new AssignTripGroupGuideRequest { GuideUserId = store.Guide1User.UserId });
        Assert.Equal(store.Guide1User.UserId, nonOverlapResult.Groups.Single().Guide!.UserId);
    }

    // 7.6 #16, #17, #18:
    // - Thay đổi group/participant đánh dấu nhu cầu đồ cấp cần tính lại; checklist chưa cập nhật thì không chốt được.
    // - Không chốt được trước ClosedForBooking, khi còn hold hợp lệ, participant chưa xếp hoặc group có người chưa có guide (group rỗng không cần guide).
    // - Group Published không nhận auto-assign và không cho chuyển/tách bằng API thường.
    [Fact]
    public async Task PublishGatekeeper_ProvidedItemsChecklist_AndPostPublishRestrictions_WorkEndToEnd()
    {
        var store = new SharedStore { OverflowSlots = 2 };
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 4, groupCount: 3, totalSlots: 12, withProvidedItems: true);
        var groups = trip.TripGroups.OrderBy(g => g.CreatedAt).ThenBy(g => g.TripGroupId).ToList();
        var staffScope = store.CreateScope(store.StaffUser);

        var booking1 = store.SeedBooking(trip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(booking1.OrderId);
        var booking2 = store.SeedBooking(trip, store.SecondBuyerUser, seatCount: 5, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(booking2.OrderId);

        var activeHold = store.SeedBooking(
            trip,
            store.BuyerUser,
            seatCount: 1,
            status: "PendingPayment",
            paymentStatus: "Unpaid",
            holdExpiresAt: BaseNow.AddMinutes(10));

        // Chặn 1: Trip còn Open => TRIP_NOT_CLOSED_FOR_BOOKING
        var exNotClosed = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Publish.PublishGroupsAsync(trip.TripId));
        Assert.Equal(ErrorCodes.TripNotClosedForBooking, exNotClosed.ErrorCode);

        await staffScope.Trips.UpdateTripStatusAsync(trip.TripId, new UpdateTripStatusRequest
        {
            Status = TripStatuses.ClosedForBooking
        });

        // Chặn 2: Còn hold thanh toán hợp lệ => ACTIVE_PAYMENT_HOLDS
        var exHold = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Publish.PublishGroupsAsync(trip.TripId));
        Assert.Equal(ErrorCodes.ActivePaymentHolds, exHold.ErrorCode);

        activeHold.HoldExpiresAt = BaseNow.AddMinutes(-1);

        // Chặn 3: Còn participant của booking2 (Paid) chưa được xếp group => UNASSIGNED_PARTICIPANTS
        var exUnassigned = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Publish.PublishGroupsAsync(trip.TripId));
        Assert.Equal(ErrorCodes.UnassignedParticipants, exUnassigned.ErrorCode);

        var b2Seats = booking2.TripBookingOrder.Participants.ToList();
        await staffScope.Grouping.SplitBookingAsync(trip.TripId, booking2.OrderId, new SplitTripBookingRequest
        {
            Reason = "Đoàn 5 người vượt sức chứa chuẩn 4",
            Assignments =
            [
                new ParticipantGroupAssignmentRequest { TripGroupId = groups[0].TripGroupId, ParticipantIds = [b2Seats[0].ParticipantId, b2Seats[1].ParticipantId] },
                new ParticipantGroupAssignmentRequest { TripGroupId = groups[1].TripGroupId, ParticipantIds = [b2Seats[2].ParticipantId, b2Seats[3].ParticipantId, b2Seats[4].ParticipantId] }
            ]
        });

        // Chặn 4: Group có người chưa có guide => GROUP_GUIDE_REQUIRED
        var exNoGuide = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Publish.PublishGroupsAsync(trip.TripId));
        Assert.Equal(ErrorCodes.GroupGuideRequired, exNoGuide.ErrorCode);

        await staffScope.Grouping.AssignGuideAsync(trip.TripId, groups[0].TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide1User.UserId
        });
        await staffScope.Grouping.AssignGuideAsync(trip.TripId, groups[1].TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide2User.UserId
        });
        // groups[2] rỗng (0 người), để GuideUserId == null

        // Chặn 5: Nhu cầu đồ cấp chưa cập nhật => EQUIPMENT_REQUIREMENTS_STALE
        Assert.True(trip.ProvidedItemsNeedsRecalc);
        var exProvided = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Publish.PublishGroupsAsync(trip.TripId));
        Assert.Equal(ErrorCodes.EquipmentRequirementsStale, exProvided.ErrorCode);

        var recalc = await staffScope.Publish.RecalculateProvidedItemsAsync(trip.TripId);
        Assert.False(recalc.ProvidedItemsNeedsRecalc);
        Assert.Equal(2, recalc.ActiveGroupCount);
        Assert.Equal(8, recalc.ActiveParticipantCount);
        Assert.Equal(4, recalc.ProvidedItems.Single(i => i.QuantityBasis == "PER_GROUP").RequiredQuantity);
        Assert.Equal(8, recalc.ProvidedItems.Single(i => i.QuantityBasis == "PER_PARTICIPANT").RequiredQuantity);

        // Chốt thành công, kể cả group[2] rỗng không có guide
        var published = await staffScope.Publish.PublishGroupsAsync(trip.TripId);
        Assert.Equal(TripGroupStatuses.Published, published.GroupingStatus);
        Assert.All(published.Groups, g => Assert.Equal(TripGroupStatuses.Published, g.Status));
        Assert.Null(published.Groups.Single(g => g.TripGroupId == groups[2].TripGroupId).Guide);

        // Sau khi Published:
        // a) Auto-assign không nhận booking mới
        var lateBooking = store.SeedBooking(trip, store.BuyerUser, seatCount: 2, status: "Confirmed", paymentStatus: "Paid");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(lateBooking.OrderId);
        Assert.All(lateBooking.TripBookingOrder.Participants, p => Assert.Null(p.TripGroupId));

        // b) Chuyển/tách/gán guide bằng API thường bị từ chối với TRIP_GROUPING_LOCKED
        var moveIntoPublishedEx = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Grouping.MoveBookingAsync(
            trip.TripId,
            lateBooking.OrderId,
            new MoveBookingGroupRequest { TargetGroupId = groups[2].TripGroupId }));
        Assert.Equal(ErrorCodes.TripGroupingLocked, moveIntoPublishedEx.ErrorCode);

        var moveFromPublishedEx = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Grouping.MoveBookingAsync(
            trip.TripId,
            booking1.OrderId,
            new MoveBookingGroupRequest { TargetGroupId = groups[2].TripGroupId }));
        Assert.Equal(ErrorCodes.TripGroupingLocked, moveFromPublishedEx.ErrorCode);

        var lateSeats = lateBooking.TripBookingOrder.Participants.ToList();
        var splitIntoPublishedEx = await Assert.ThrowsAsync<ConflictException>(() => staffScope.Grouping.SplitBookingAsync(
            trip.TripId,
            lateBooking.OrderId,
            new SplitTripBookingRequest
            {
                Reason = "Cố tách vào group đã chốt",
                Assignments =
                [
                    new ParticipantGroupAssignmentRequest { TripGroupId = groups[1].TripGroupId, ParticipantIds = [lateSeats[0].ParticipantId] },
                    new ParticipantGroupAssignmentRequest { TripGroupId = groups[2].TripGroupId, ParticipantIds = [lateSeats[1].ParticipantId] }
                ]
            }));
        Assert.Equal(ErrorCodes.TripGroupingLocked, splitIntoPublishedEx.ErrorCode);
    }

    // 7.6 #19 & #20:
    // - Guide thấy group Draft ngay sau khi được gán (không lộ group khác hoặc unassignedBookings) và mất quyền khi bị đổi khỏi group.
    // - Khách nhận groupingStatus = Pending khi group còn Draft; sau khi Published, chủ Order thấy các participant thuộc booking của mình (kể cả khi bị tách nhiều group),
    //   còn tài khoản người đi cùng chỉ thấy group và đúng participant đã liên kết với chính họ; không lộ dữ liệu của booking khác.
    [Fact]
    public async Task GuideAndCustomerVisibility_RespectsDraftAndPublishedBoundaries_SplitBookings_AndCompanionIsolation()
    {
        var store = new SharedStore { OverflowSlots = 2 };
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 4, groupCount: 2, totalSlots: 10);
        var groups = trip.TripGroups.OrderBy(g => g.CreatedAt).ThenBy(g => g.TripGroupId).ToList();
        var staffScope = store.CreateScope(store.StaffUser);

        var strangerBooking = store.SeedBooking(
            trip,
            store.SecondBuyerUser,
            seatCount: 2,
            status: "Confirmed",
            paymentStatus: "Paid",
            namePrefix: "Người Lạ");
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(strangerBooking.OrderId);

        var buyerBooking = store.SeedBooking(
            trip,
            store.BuyerUser,
            seatCount: 3,
            status: "Confirmed",
            paymentStatus: "Paid",
            namePrefix: "Đoàn Buyer");
        var buyerSeats = buyerBooking.TripBookingOrder.Participants.ToList();
        buyerSeats[0].FullName = "Chủ Đơn";
        buyerSeats[0].LinkedUserId = store.BuyerUser.UserId;
        buyerSeats[0].LinkedUser = store.BuyerUser;

        buyerSeats[1].FullName = "Bạn Đồng Hành";
        buyerSeats[1].Email = store.CompanionUser.Email;
        buyerSeats[1].LinkedUserId = store.CompanionUser.UserId;
        buyerSeats[1].LinkedUser = store.CompanionUser;

        buyerSeats[2].FullName = "Bé Con";

        await staffScope.Grouping.SplitBookingAsync(trip.TripId, buyerBooking.OrderId, new SplitTripBookingRequest
        {
            Reason = "Ghép đoàn theo sức chứa",
            Assignments =
            [
                new ParticipantGroupAssignmentRequest { TripGroupId = groups[0].TripGroupId, ParticipantIds = [buyerSeats[0].ParticipantId, buyerSeats[2].ParticipantId] },
                new ParticipantGroupAssignmentRequest { TripGroupId = groups[1].TripGroupId, ParticipantIds = [buyerSeats[1].ParticipantId] }
            ]
        });

        await staffScope.Grouping.AssignGuideAsync(trip.TripId, groups[0].TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide1User.UserId
        });

        // 1) Guide1 thấy ngay group[0] ở trạng thái Draft (cả qua /grouping lẫn /guide/trip-groups), KHÔNG thấy group[1] ngoài phạm vi
        var guide1Scope = store.CreateScope(store.Guide1User);
        var guide1Snapshot = await guide1Scope.Grouping.GetGroupingSnapshotAsync(trip.TripId);
        var onlyVisibleGroup = Assert.Single(guide1Snapshot.Groups);
        Assert.Equal(groups[0].TripGroupId, onlyVisibleGroup.TripGroupId);
        Assert.Equal(TripGroupStatuses.Draft, onlyVisibleGroup.Status);
        Assert.Equal(4, onlyVisibleGroup.ParticipantCount);
        Assert.Empty(guide1Snapshot.UnassignedBookings);

        var guide1List = await guide1Scope.Views.GetGuideGroupsAsync();
        Assert.Single(guide1List);

        // 2) Staff đổi guide của group[0] từ Guide1 sang Guide2 => Guide1 mất quyền ngay lập tức!
        await staffScope.Grouping.AssignGuideAsync(trip.TripId, groups[0].TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide2User.UserId
        });
        Assert.Empty(await guide1Scope.Views.GetGuideGroupsAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => guide1Scope.Grouping.GetGroupingSnapshotAsync(trip.TripId));
        await Assert.ThrowsAsync<NotFoundException>(() => guide1Scope.Views.GetGuideGroupByIdAsync(groups[0].TripGroupId));

        await staffScope.Grouping.AssignGuideAsync(trip.TripId, groups[1].TripGroupId, new AssignTripGroupGuideRequest
        {
            GuideUserId = store.Guide1User.UserId
        });

        // 3) Khi group còn Draft: cả Chủ đơn lẫn Bạn đồng hành nhận groupingStatus = Pending và groups = []
        var buyerScope = store.CreateScope(store.BuyerUser);
        var companionScope = store.CreateScope(store.CompanionUser);
        var pendingBuyerView = await buyerScope.Views.GetCustomerGroupingAsync(buyerBooking.OrderId);
        var pendingCompanionView = await companionScope.Views.GetCustomerGroupingAsync(buyerBooking.OrderId);
        Assert.Equal(TripGroupStatuses.Pending, pendingBuyerView.GroupingStatus);
        Assert.Empty(pendingBuyerView.Groups);
        Assert.Equal(TripGroupStatuses.Pending, pendingCompanionView.GroupingStatus);
        Assert.Empty(pendingCompanionView.Groups);

        // 4) Staff đóng nhận khách, tính lại đồ cấp và Chốt (Published)
        await staffScope.Trips.UpdateTripStatusAsync(trip.TripId, new UpdateTripStatusRequest
        {
            Status = TripStatuses.ClosedForBooking
        });
        await staffScope.Publish.RecalculateProvidedItemsAsync(trip.TripId);
        await staffScope.Publish.PublishGroupsAsync(trip.TripId);

        // 5) Sau khi Published:
        var buyerGrouping = await buyerScope.Views.GetCustomerGroupingAsync(buyerBooking.OrderId);
        Assert.Equal(TripGroupStatuses.Published, buyerGrouping.GroupingStatus);
        Assert.Equal(2, buyerGrouping.Groups.Count);
        var buyerViewGroup0 = buyerGrouping.Groups.Single(g => g.TripGroupId == groups[0].TripGroupId);
        var buyerViewGroup1 = buyerGrouping.Groups.Single(g => g.TripGroupId == groups[1].TripGroupId);
        Assert.Equal(store.Guide2User.FullName, buyerViewGroup0.Guide!.FullName);
        Assert.Equal(["Chủ Đơn", "Bé Con"], buyerViewGroup0.Participants.Select(p => p.FullName).ToArray());
        Assert.DoesNotContain(buyerViewGroup0.Participants, p => p.FullName.StartsWith("Người Lạ", StringComparison.Ordinal));
        Assert.Equal(["Bạn Đồng Hành"], buyerViewGroup1.Participants.Select(p => p.FullName).ToArray());

        var companionGrouping = await companionScope.Views.GetCustomerGroupingAsync(buyerBooking.OrderId);
        Assert.Equal(TripGroupStatuses.Published, companionGrouping.GroupingStatus);
        var onlyCompanionGroup = Assert.Single(companionGrouping.Groups);
        Assert.Equal(groups[1].TripGroupId, onlyCompanionGroup.TripGroupId);
        var onlyCompanionSeat = Assert.Single(onlyCompanionGroup.Participants);
        Assert.Equal("Bạn Đồng Hành", onlyCompanionSeat.FullName);
    }

    // 7.6 #21: Booking Paid không thêm hoặc xóa participant; booking bổ sung được tạo riêng và Staff có thể xếp chung nếu còn sức chứa.
    [Fact]
    public async Task PaidBooking_CannotAddOrRemoveParticipants_AndSupplementaryBookingCanBeMergedByStaffWithinCapacity()
    {
        var store = new SharedStore { OverflowSlots = 2 };
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 4, groupCount: 2, totalSlots: 10);
        var groups = trip.TripGroups.OrderBy(g => g.CreatedAt).ThenBy(g => g.TripGroupId).ToList();
        var buyerScope = store.CreateScope(store.BuyerUser);
        var staffScope = store.CreateScope(store.StaffUser);

        var serviceMethods = typeof(ITripBookingService).GetMethods().Select(m => m.Name).ToArray();
        Assert.DoesNotContain(serviceMethods, name => name.Contains("AddParticipant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(serviceMethods, name => name.Contains("RemoveParticipant", StringComparison.OrdinalIgnoreCase) ||
                                                     name.Contains("DeleteParticipant", StringComparison.OrdinalIgnoreCase));

        var initialBooking = await buyerScope.TripBookings.CreateAsync(new CreateTripBookingRequest
        {
            AcceptedRefundPolicyVersion = 1,
            TripId = trip.TripId,
            ContactPhone = "0901112222",
            BuyerParticipates = true,
            AdditionalParticipants =
            [
                new CreateBookingParticipantRequest { FullName = "Bạn 1", Email = "b1@example.com" },
                new CreateBookingParticipantRequest { FullName = "Bạn 2", Email = "b2@example.com" }
            ]
        }, "booking-initial");

        var initialOrder = store.Orders.Single(o => o.OrderId == initialBooking.OrderId);
        initialOrder.Status = OrderStatuses.Confirmed;
        initialOrder.PaymentStatus = OrderPaymentStatuses.Paid;
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(initialOrder.OrderId);

        var initialGroupId = initialOrder.TripBookingOrder.Participants.First().TripGroupId!.Value;
        Assert.Equal(groups[0].TripGroupId, initialGroupId);
        Assert.Equal(3, initialOrder.TripBookingOrder.Participants.Count);

        await Assert.ThrowsAsync<NotFoundException>(() => buyerScope.TripBookings.UpdateParticipantAsync(
            initialOrder.OrderId,
            Guid.NewGuid(),
            new UpdateBookingParticipantRequest { FullName = "Người cố thêm vào đơn Paid" }));
        Assert.Equal(3, initialOrder.TripBookingOrder.Participants.Count);

        var extraBooking = await buyerScope.TripBookings.CreateAsync(new CreateTripBookingRequest
        {
            AcceptedRefundPolicyVersion = 1,
            TripId = trip.TripId,
            ContactPhone = "0901112222",
            BuyerParticipates = false,
            AdditionalParticipants =
            [
                new CreateBookingParticipantRequest { FullName = "Người Bổ Sung 1", Email = "extra1@example.com" },
                new CreateBookingParticipantRequest { FullName = "Người Bổ Sung 2", Email = "extra2@example.com" }
            ]
        }, "booking-extra");

        var extraOrder = store.Orders.Single(o => o.OrderId == extraBooking.OrderId);
        extraOrder.Status = OrderStatuses.Confirmed;
        extraOrder.PaymentStatus = OrderPaymentStatuses.Paid;
        await staffScope.Grouping.AutoAssignPaidBookingInTransactionAsync(extraOrder.OrderId);
        Assert.All(extraOrder.TripBookingOrder.Participants, p => Assert.Equal(groups[1].TripGroupId, p.TripGroupId));

        var merged = await staffScope.Grouping.MoveBookingAsync(trip.TripId, extraOrder.OrderId, new MoveBookingGroupRequest
        {
            TargetGroupId = groups[0].TripGroupId,
            Note = "Ghép booking bổ sung đi cùng đoàn gốc"
        });

        var mergedGroup0 = merged.Groups.Single(g => g.TripGroupId == groups[0].TripGroupId);
        Assert.Equal(5, mergedGroup0.ParticipantCount);
        Assert.All(extraOrder.TripBookingOrder.Participants, p => Assert.Equal(groups[0].TripGroupId, p.TripGroupId));
    }

    // Kanban (FR-D06, BR-10, FR-I01): hai staff cùng kéo khách vào chỗ cuối của một nhóm thì chỉ một người thành công;
    // thao tác thành công ghi audit và bật cờ tính lại đồ cấp.
    [Fact]
    public async Task KanbanAssignParticipant_ConcurrentMovesIntoLastSlot_OnlyOneSucceedsWithAuditAndRecalc()
    {
        var store = new SharedStore();
        var trip = await CreateOpenTripAsync(store, groupMaxParticipants: 2, groupCount: 2, totalSlots: 4);
        var order = store.SeedBooking(trip, store.BuyerUser, seatCount: 3, status: "Confirmed", paymentStatus: "Paid");
        var target = trip.TripGroups.First();
        var participants = order.TripBookingOrder.Participants.ToList();
        participants[0].TripGroupId = target.TripGroupId;
        trip.ProvidedItemsNeedsRecalc = false;
        var rowVersionBefore = trip.RowVersion;
        store.SimulateConcurrentReadDelay = true;

        var moves = participants.Skip(1).Select(p => Task.Run(() => store.CreateScope(store.StaffUser).Kanban.AssignParticipantGroupAsync(
            trip.TripId,
            new AssignParticipantGroupRequest { ParticipantId = p.ParticipantId, TargetGroupId = target.TripGroupId }))).ToList();
        var results = await Task.WhenAll(moves.Select(async t =>
        {
            try { await t; return null; }
            catch (ConflictException ex) { return ex.ErrorCode; }
        }));

        Assert.Equal(2, participants.Count(p => p.TripGroupId == target.TripGroupId));
        Assert.Single(results, r => r is null);
        Assert.Single(results, r => r == ErrorCodes.GroupCapacityExceeded);
        Assert.Single(store.AuditLogs, a => a.Action == "KanbanMoveParticipant" && a.ActorUserId == store.StaffUser.UserId);
        Assert.True(trip.ProvidedItemsNeedsRecalc);
        Assert.Equal(rowVersionBefore + 1, trip.RowVersion);
    }

    private static async Task<Trip> CreateOpenTripAsync(
        SharedStore store,
        int groupMaxParticipants,
        int groupCount,
        int totalSlots,
        DateTimeOffset? startAt = null,
        DateTimeOffset? endAt = null,
        bool withProvidedItems = false)
    {
        var tour = store.SeedTour(groupMaxParticipants, withProvidedItems);
        var adminScope = store.CreateScope(store.AdminUser);
        var created = await adminScope.Trips.CreateTripAsync(new CreateTripRequest
        {
            TourId = tour.Id,
            StartAt = startAt ?? BaseNow.AddDays(10),
            EndAt = endAt ?? BaseNow.AddDays(12),
            TotalSlots = totalSlots,
            GroupCount = groupCount,
            PricePerPerson = 1_500_000m,
            MinParticipants = 1
        });
        await adminScope.Trips.UpdateTripStatusAsync(created.TripId, new UpdateTripStatusRequest
        {
            Status = TripStatuses.Open
        });
        // Staff fixture vận hành chuyến (PRD §3.2 "G": chỉ chuyến mình phụ trách).
        var trip = store.Trips.Single(t => t.TripId == created.TripId);
        trip.ManagedByUserId = store.StaffUser.UserId;
        return trip;
    }

    private sealed record ServiceScope(
        IUnitOfWork UnitOfWork,
        TripRecurrenceService Recurrence,
        TripService Trips,
        TripGroupingService Grouping,
        TripGroupPublishService Publish,
        TripGroupViewService Views,
        TripBookingService TripBookings,
        AdminTripGroupService Kanban);

    private sealed class SharedStore
    {
        public int OverflowSlots { get; set; } = 2;
        public bool SimulateConcurrentReadDelay { get; set; }
        public Guid RefundPolicyId { get; } = Guid.NewGuid();

        public List<Account> Accounts { get; } = [];
        public List<Tour> Tours { get; } = [];
        public List<TourRecurrenceRule> RecurrenceRules { get; } = [];
        public List<Trip> Trips { get; } = [];
        public List<TripGroup> Groups { get; } = [];
        public List<TripProvidedItem> TripProvidedItems { get; } = [];
        public List<Order> Orders { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public List<Notification> Notifications { get; } = [];

        public ConcurrentDictionary<string, SemaphoreSlim> AdvisoryLocks { get; } = new();

        public Account AdminUser { get; }
        public Account StaffUser { get; }
        public Account Guide1User { get; }
        public Account Guide2User { get; }
        public Account BuyerUser { get; }
        public Account SecondBuyerUser { get; }
        public Account CompanionUser { get; }

        public SharedStore()
        {
            AdminUser = SeedAccount("admin@rentrek.vn", "Quản trị viên", RoleNames.Admin);
            StaffUser = SeedAccount("staff@rentrek.vn", "Điều phối viên", RoleNames.Staff);
            Guide1User = SeedAccount("guide1@rentrek.vn", "HDV Minh", RoleNames.Guide, "0911000111");
            Guide2User = SeedAccount("guide2@rentrek.vn", "HDV Lan", RoleNames.Guide, "0911000222");
            BuyerUser = SeedAccount("buyer1@rentrek.vn", "Khách Mua 1", RoleNames.Customer, "0901112222");
            SecondBuyerUser = SeedAccount("buyer2@rentrek.vn", "Khách Mua 2", RoleNames.Customer, "0903334444");
            CompanionUser = SeedAccount("companion@rentrek.vn", "Bạn Đồng Hành", RoleNames.Customer, "0905556666");
        }

        public Account SeedAccount(string email, string fullName, string roleName, string phone = "0900000000")
        {
            var account = new Account
            {
                UserId = Guid.NewGuid(),
                Email = email,
                FullName = fullName,
                Phone = phone,
                Status = "Active",
                EmailConfirmedAt = BaseNow.UtcDateTime,
                Role = new Role { RoleId = Guid.NewGuid(), RoleName = roleName }
            };
            Accounts.Add(account);
            return account;
        }

        public Tour SeedTour(int? groupMaxParticipants, bool withProvidedItems = false)
        {
            var tour = new Tour
            {
                Id = Guid.NewGuid(),
                Name = "Tà Năng - Phan Dũng",
                DurationDays = 2,
                DurationNights = 1,
                DurationMinutes = 2880,
                BasePrice = 2_000_000m,
                GroupMaxParticipants = groupMaxParticipants,
                CreatedByUserId = AdminUser.UserId,
                Status = "Active",
                Activities =
                [
                    new TourActivity
                    {
                        Id = Guid.NewGuid(),
                        DayNumber = 1,
                        DayTitle = "Ngày 1",
                        StartTime = new TimeSpan(7, 0, 0),
                        DurationMinutes = 180,
                        DisplayOrder = 1,
                        Name = "Xuất phát cửa rừng"
                    }
                ]
            };

            if (withProvidedItems)
            {
                tour.ProvidedItems =
                [
                    new TourProvidedItem
                    {
                        Id = Guid.NewGuid(),
                        TourId = tour.Id,
                        Tour = tour,
                        ProductVariantId = Guid.NewGuid(),
                        ItemName = "Lều chữ A",
                        Quantity = 2,
                        QuantityBasis = "PER_GROUP"
                    },
                    new TourProvidedItem
                    {
                        Id = Guid.NewGuid(),
                        TourId = tour.Id,
                        Tour = tour,
                        ProductVariantId = Guid.NewGuid(),
                        ItemName = "Túi ngủ giữ nhiệt",
                        Quantity = 1,
                        QuantityBasis = "PER_PARTICIPANT"
                    }
                ];
            }

            Tours.Add(tour);
            return tour;
        }

        public Order SeedBooking(
            Trip trip,
            Account buyer,
            int seatCount,
            string status,
            string paymentStatus,
            DateTimeOffset? holdExpiresAt = null,
            string namePrefix = "Khách")
        {
            var orderId = Guid.NewGuid();
            var booking = new TripBookingOrder
            {
                OrderId = orderId,
                TripId = trip.TripId,
                Trip = trip,
                ParticipantCount = seatCount,
                UnitPrice = trip.PricePerPerson,
                GroupingPreference = "KeepTogether"
            };
            var order = new Order
            {
                OrderId = orderId,
                CustomerUserId = buyer.UserId,
                CustomerUser = buyer,
                OrderCode = $"RT-{orderId.ToString("N")[..8].ToUpperInvariant()}",
                OrderType = OrderTypes.TripBooking,
                SubtotalAmount = seatCount * trip.PricePerPerson,
                TotalAmount = seatCount * trip.PricePerPerson,
                Status = status,
                PaymentStatus = paymentStatus,
                HoldExpiresAt = holdExpiresAt ?? BaseNow.AddMinutes(15),
                RefundPolicyId = trip.RefundPolicyId,
                CreatedAt = BaseNow,
                UpdatedAt = BaseNow,
                TripBookingOrder = booking
            };
            booking.Order = order;

            for (var i = 1; i <= seatCount; i++)
            {
                booking.Participants.Add(new BookingParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    BookingOrderId = orderId,
                    BookingOrder = booking,
                    TripId = trip.TripId,
                    Trip = trip,
                    FullName = $"{namePrefix} {i}",
                    Email = $"seat{i}.{orderId:N}@example.com",
                    Phone = "0909998888",
                    Status = "Confirmed",
                    CreatedAt = BaseNow.AddSeconds(i),
                    UpdatedAt = BaseNow.AddSeconds(i)
                });
            }

            trip.TripBookingOrders.Add(booking);
            Orders.Add(order);
            return order;
        }

        public ServiceScope CreateScope(Account? account)
        {
            var uow = new ScopedFakeUnitOfWork(this);
            var currentUser = new FakeCurrentUser(account);
            var time = new FixedTimeProvider(BaseNow);
            var recurrence = new TripRecurrenceService(uow, currentUser, time, NullLogger<TripRecurrenceService>.Instance);
            var tripBookingService = new TripBookingService(
                uow,
                currentUser,
                time,
                new FakeInviteTokenHelper(),
                Microsoft.Extensions.Options.Options.Create(new EmailSettings
                {
                    FrontendBaseUrl = "http://localhost:5173"
                }),
                TestProxy.Create<INotificationService>(),
                TestProxy.NoRefunds());
            return new ServiceScope(uow, recurrence, new TripService(uow, currentUser, time), new TripGroupingService(uow, currentUser, time),
                new TripGroupPublishService(uow, currentUser, time), new TripGroupViewService(uow, currentUser, time), tripBookingService,
                new AdminTripGroupService(uow, currentUser, time));
        }
    }

    private sealed class FakeCurrentUser(Account? account) : ICurrentUserService
    {
        public Guid? UserId => account?.UserId;
        public Guid RequiredUserId => account?.UserId ?? throw new UnauthorizedException("Chưa đăng nhập");
        public string? Jti => null;
        public string? Role => account?.Role?.RoleName;
        public bool IsAuthenticated => account is not null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeInviteTokenHelper : IInviteTokenHelper
    {
        public string CreateGroupToken(Guid orderId, int version) => $"t:{orderId:N}:{version}";
        public string CreateEmailVerificationToken(Guid userId, string normalizedEmail, DateTimeOffset expiresAt) => throw new NotSupportedException();
        public bool TryParseEmailVerificationToken(string? token, out Guid userId, out DateTimeOffset expiresAt) => throw new NotSupportedException();
        public bool VerifyEmailVerificationToken(string? token, Guid userId, string normalizedEmail, DateTimeOffset utcNow) => throw new NotSupportedException();
    }

    private sealed class ScopedFakeUnitOfWork : IUnitOfWork
    {
        public IAdminOperationRepository AdminOperations => throw new NotSupportedException();
        private readonly SharedStore _store;
        private readonly List<SemaphoreSlim> _heldLocks = [];
        private readonly HashSet<string> _heldLockKeys = [];

        public ScopedFakeUnitOfWork(SharedStore store)
        {
            _store = store;
            TripGroups = new FakeTripGroupRepository(store, AcquireLockAsync);
            TripBookings = new FakeTripBookingRepository(store, AcquireLockAsync);
            SystemConfigurations = new FakeSystemConfigurationRepository(store);
            TourRecurrenceRules = new FakeListRepository<TourRecurrenceRule>(store.RecurrenceRules);
            Trips = new FakeListRepository<Trip>(store.Trips);
            TripProvidedItems = new FakeTripProvidedItemRepository(store.TripProvidedItems);
            AuditLogs = new FakeAuditLogRepository(store.AuditLogs);
            Notifications = new FakeNotificationRepository(store.Notifications);
        }

        public ITripGroupRepository TripGroups { get; }
        public ITripBookingRepository TripBookings { get; }
        public ISystemConfigurationRepository SystemConfigurations { get; }
        public ITourRecurrenceRuleRepository TourRecurrenceRules { get; }
        public ITripRepository Trips { get; }
        public ITripProvidedItemRepository TripProvidedItems { get; }
        public IAuditLogRepository AuditLogs { get; }
        public INotificationRepository Notifications { get; }

        public IAccessTokenRepository AccessTokens => throw new NotSupportedException();
        public IAccountRepository Accounts => throw new NotSupportedException();
        public IActivityAttendanceRepository ActivityAttendances => throw new NotSupportedException();
        public IAppliedPromotionRepository AppliedPromotions => throw new NotSupportedException();
        public IAssetAssignmentRepository AssetAssignments => throw new NotSupportedException();
        public IBookingParticipantRepository BookingParticipants => throw new NotSupportedException();
        public ICategoryRepository Categories => throw new NotSupportedException();
        public IEquipmentAssetRepository EquipmentAssets => throw new NotSupportedException();
        public ILocationRepository Locations => throw new NotSupportedException();
        public IOrderRepository Orders => throw new NotSupportedException();
        public IParticipantPostReadRepository ParticipantPostReads => throw new NotSupportedException();
        public IPasswordResetTokenRepository PasswordResetTokens => throw new NotSupportedException();
        public IPaymentRepository Payments { get; } = TestProxy.Create<IPaymentRepository>(
            ("AcquireOrderLockAsync", _ => Task.CompletedTask),
            ("GetLatestPendingCollectionAsync", _ => Task.FromResult<Payment?>(null)));
        public IPostRepository Posts => throw new NotSupportedException();
        public IPostSoftSkillRepository PostSoftSkills => throw new NotSupportedException();
        public IPostTagRepository PostTags => throw new NotSupportedException();
        public IProductRepository Products => throw new NotSupportedException();
        public IProductBundleRepository ProductBundles => throw new NotSupportedException();
        public IProductBundleItemRepository ProductBundleItems => throw new NotSupportedException();
        public IProductVariantRepository ProductVariants => throw new NotSupportedException();
        public IProductVariantImageRepository ProductVariantImages => throw new NotSupportedException();
        public IProfileRepository Profiles => throw new NotSupportedException();
        public IPromotionRepository Promotions => throw new NotSupportedException();
        public IPromotionTierRepository PromotionTiers => throw new NotSupportedException();
        public IRefreshTokenRepository RefreshTokens => throw new NotSupportedException();
        public IRefundCaseRepository RefundCases => throw new NotSupportedException();
        public IRefundPolicyRepository RefundPolicies => TestProxy.Create<IRefundPolicyRepository>(
            ("GetDetailAsync", args => Task.FromResult<RefundPolicy?>((Guid)args[0]! == _store.RefundPolicyId
                ? new RefundPolicy
                {
                    RefundPolicyId = _store.RefundPolicyId,
                    PolicyType = RefundPolicyTypes.Tour,
                    Status = RefundPolicyStatuses.Active,
                    Version = RefundPolicyVersion
                }
                : null)),
            ("GetCurrentAsync", _ => Task.FromResult<RefundPolicy?>(null)));
        public int RefundPolicyVersion { get; set; } = 1;
        public IRefundPolicyRuleRepository RefundPolicyRules => throw new NotSupportedException();
        public IRentalBundleItemSnapshotRepository RentalBundleItemSnapshots => throw new NotSupportedException();
        public IRentalChargeRepository RentalCharges => throw new NotSupportedException();
        public IRentalOrderRepository RentalOrders => throw new NotSupportedException();
        public IRentalOrderItemRepository RentalOrderItems => throw new NotSupportedException();
        public IRentalPriceTierRepository RentalPriceTiers => throw new NotSupportedException();
        public IRentalPolicyRepository RentalPolicies => throw new NotSupportedException();
        public IRentalEvidencePhotoRepository RentalEvidencePhotos => throw new NotSupportedException();
        public IRentalReportRepository RentalReports => throw new NotSupportedException();
        public IRentalSettlementRepository RentalSettlements => throw new NotSupportedException();
        public IReviewRepository Reviews => throw new NotSupportedException();
        public IRoleRepository Roles => throw new NotSupportedException();
        public ISoftSkillRepository SoftSkills => throw new NotSupportedException();
        public ITagRepository Tags => throw new NotSupportedException();
        public ITourRepository Tours { get; } = new ActiveTourRepositoryStub();
        public ITourActivityRepository TourActivities => throw new NotSupportedException();
        public ITourImageRepository TourImages => throw new NotSupportedException();
        public ITourPostRepository TourPosts => throw new NotSupportedException();
        public ITourPreparationItemRepository TourPreparationItems => throw new NotSupportedException();
        public ITourProvidedItemRepository TourProvidedItems => throw new NotSupportedException();
        public ITripBookingOrderRepository TripBookingOrders => throw new NotSupportedException();
        public IUserExternalLoginRepository UserExternalLogins => throw new NotSupportedException();
        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class => throw new NotSupportedException();

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<bool> TrySaveChangesIgnoringUniqueViolationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ReleaseLocks();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            ReleaseLocks();
            return Task.CompletedTask;
        }

        public void Dispose() => ReleaseLocks();

        private async Task AcquireLockAsync(string key, CancellationToken cancellationToken)
        {
            if (!_heldLockKeys.Add(key))
                return;

            var sem = _store.AdvisoryLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(cancellationToken);
            _heldLocks.Add(sem);
        }

        private void ReleaseLocks()
        {
            for (var i = _heldLocks.Count - 1; i >= 0; i--)
                _heldLocks[i].Release();
            _heldLocks.Clear();
            _heldLockKeys.Clear();
        }
    }

    private sealed class FakeTripGroupRepository(
        SharedStore store,
        Func<string, CancellationToken, Task> lockAcquirer) : ITripGroupRepository
    {
        Task<IReadOnlyList<BookingParticipant>> ITripGroupRepository.GetActiveParticipantsForUpdateByTripAsync(
                Guid tripId,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<BookingParticipant>> ITripGroupRepository.GetActiveParticipantsWithOrderByTripAsync(
                Guid tripId,
                CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BookingParticipant>>(TripParticipants(tripId).ToList());
        Task<TripBookingOrder?> ITripGroupRepository.GetBookingOrderForUpdateAsync(
                Guid tripId,
                Guid bookingId,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<TripGroup?> ITripGroupRepository.GetGroupForUpdateAsync(Guid tripId, Guid groupId, CancellationToken cancellationToken)
            => Task.FromResult(store.Trips.Single(t => t.TripId == tripId).TripGroups
                .SingleOrDefault(g => g.TripGroupId == groupId && !g.IsDeleted));

        async Task<int> ITripGroupRepository.GetGroupParticipantCountAsync(Guid groupId, CancellationToken cancellationToken)
        {
            var count = store.Trips.SelectMany(t => TripParticipants(t.TripId)).Count(p => p.TripGroupId == groupId);
            if (store.SimulateConcurrentReadDelay)
                await Task.Delay(25, cancellationToken);
            return count;
        }
        Task<IReadOnlyList<TripGroup>> ITripGroupRepository.GetGroupsForUpdateByTripAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripGroup>> ITripGroupRepository.GetGroupsWithDetailsByTripAsync(Guid tripId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TripGroup>>(store.Trips.Single(t => t.TripId == tripId).TripGroups.Where(g => !g.IsDeleted).ToList());
        Task<IReadOnlyList<TripGroup>> ITripGroupRepository.GetGuideAssignedGroupsWithTripAndLocationAsync(
                Guid guideUserId,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<TripGroup?> ITripGroupRepository.GetGuideGroupForTripAsync(
                Guid tripId,
                Guid guideUserId,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Account?> ITripGroupRepository.GetGuideWithRoleAndProfileAsync(Guid guideUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<BookingParticipant?> ITripGroupRepository.GetParticipantForUpdateAsync(
                Guid tripId,
                Guid participantId,
                CancellationToken cancellationToken)
            => Task.FromResult(TripParticipants(tripId).SingleOrDefault(p => p.ParticipantId == participantId));

        private IEnumerable<BookingParticipant> TripParticipants(Guid tripId)
            => store.Trips.Single(t => t.TripId == tripId).TripBookingOrders
                .SelectMany(b => b.Participants)
                .Where(p => !p.IsDeleted && p.Status != BookingParticipantStatuses.Cancelled);
        Task<Trip?> ITripGroupRepository.GetTripAsync(Guid tripId, CancellationToken cancellationToken)
            => Task.FromResult(store.Trips.SingleOrDefault(t => t.TripId == tripId && !t.IsDeleted));
        Task<bool> ITripGroupRepository.GroupNameExistsInTripAsync(
                Guid tripId,
                string groupName,
                Guid? excludeGroupId,
                CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AcquireTripLockAsync(Guid tripId, CancellationToken cancellationToken = default)
            => lockAcquirer($"trip-group:trip:{tripId:N}", cancellationToken);

        public Task AcquireGuideLockAsync(Guid guideUserId, CancellationToken cancellationToken = default)
            => lockAcquirer($"trip-group:guide:{guideUserId:N}", cancellationToken);

        public Task AcquireRecurrenceRuleLockAsync(Guid recurrenceRuleId, CancellationToken cancellationToken = default)
            => lockAcquirer($"trip-group:recurrence:{recurrenceRuleId:N}", cancellationToken);

        public Task<Tour?> GetTourForWriteAsync(Guid tourId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Tours.SingleOrDefault(t => t.Id == tourId && !t.IsDeleted));

        public Task<IReadOnlyList<TourRecurrenceRule>> GetRecurrenceRulesByTourAsync(Guid tourId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TourRecurrenceRule>>(store.RecurrenceRules.Where(r => r.TourId == tourId && !r.IsDeleted).ToList());

        public Task<IReadOnlyList<TourRecurrenceRule>> GetActiveRecurrenceRulesAsync(Guid? tourId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TourRecurrenceRule>>(store.RecurrenceRules
                .Where(r => r.IsActive && !r.IsDeleted && (!tourId.HasValue || r.TourId == tourId.Value))
                .ToList());

        public Task<Guid?> GetActiveTourRefundPolicyIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(store.RefundPolicyId);

        public Task<Guid?> GetDefaultManagerUserIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(store.AdminUser.UserId);

        public Task<IReadOnlyList<Guid>> GetAdminAccountIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([store.AdminUser.UserId]);

        public async Task<Trip?> GetTripWorkspaceGraphAsync(Guid tripId, bool asTracking = true, CancellationToken cancellationToken = default)
        {
            if (store.SimulateConcurrentReadDelay)
                await Task.Delay(25, cancellationToken);

            var trip = store.Trips.SingleOrDefault(t => t.TripId == tripId && !t.IsDeleted);
            if (trip is not null)
                SyncGraph(store, trip);
            return trip;
        }

        public Task<Guid?> GetBookingTripIdAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Orders.SingleOrDefault(o => o.OrderId == orderId)?.TripBookingOrder?.TripId);

        public Task<Order?> GetBookingWithOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Orders.SingleOrDefault(o => o.OrderId == orderId && !o.IsDeleted));

        public Task<Account?> GetAccountWithRoleAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Accounts.SingleOrDefault(a => a.UserId == userId && !a.IsDeleted));

        public Task<bool> HasOverlappingGuideAssignmentAsync(
            Guid guideUserId,
            Guid excludeTripId,
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            CancellationToken cancellationToken = default)
            => Task.FromResult(store.Groups.Any(g =>
                !g.IsDeleted &&
                g.GuideUserId == guideUserId &&
                g.TripId != excludeTripId &&
                !g.Trip.IsDeleted &&
                g.Trip.Status != TripStatuses.Cancelled &&
                g.Trip.StartAt < endAt &&
                startAt < g.Trip.EndAt));

        public Task<IReadOnlyList<TripGroup>> GetGuideGroupsAsync(Guid guideUserId, CancellationToken cancellationToken = default)
        {
            foreach (var trip in store.Trips)
                SyncGraph(store, trip);

            var groups = store.Groups
                .Where(g => !g.IsDeleted && g.GuideUserId == guideUserId && !g.Trip.IsDeleted && g.Trip.Status != TripStatuses.Cancelled)
                .ToList();
            return Task.FromResult<IReadOnlyList<TripGroup>>(groups);
        }

        public Task<TripGroup?> GetGuideGroupByIdAsync(Guid groupId, Guid guideUserId, CancellationToken cancellationToken = default)
        {
            foreach (var trip in store.Trips)
                SyncGraph(store, trip);

            var group = store.Groups.SingleOrDefault(g =>
                g.TripGroupId == groupId &&
                !g.IsDeleted &&
                g.GuideUserId == guideUserId &&
                !g.Trip.IsDeleted &&
                g.Trip.Status != TripStatuses.Cancelled);
            return Task.FromResult(group);
        }

        public Task AddAsync(TripGroup entity, CancellationToken cancellationToken = default)
        {
            if (store.Groups.All(x => x.TripGroupId != entity.TripGroupId))
                store.Groups.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<TripGroup> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                if (store.Groups.All(x => x.TripGroupId != entity.TripGroupId))
                    store.Groups.Add(entity);
            }
            return Task.CompletedTask;
        }

        public Task<TripGroup?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(TripGroup entity) { }
        public void Remove(TripGroup entity) => store.Groups.Remove(entity);
        public void RemoveRange(IEnumerable<TripGroup> entities) { foreach (var e in entities) store.Groups.Remove(e); }

        private static void SyncGraph(SharedStore store, Trip trip)
        {
            foreach (var booking in trip.TripBookingOrders)
            {
                foreach (var participant in booking.Participants)
                {
                    participant.BookingOrder = booking;
                    participant.Trip = trip;
                    if (participant.TripGroupId.HasValue)
                    {
                        var grp = trip.TripGroups.FirstOrDefault(g => g.TripGroupId == participant.TripGroupId.Value);
                        participant.TripGroup = grp;
                        if (grp is not null && grp.BookingParticipants.All(x => x.ParticipantId != participant.ParticipantId))
                            grp.BookingParticipants.Add(participant);
                    }
                    else
                    {
                        participant.TripGroup = null;
                    }
                }
            }

            foreach (var grp in trip.TripGroups)
            {
                if (grp.GuideUserId.HasValue)
                    grp.GuideUser = store.Accounts.FirstOrDefault(a => a.UserId == grp.GuideUserId.Value);
                else
                    grp.GuideUser = null;
            }
        }
    }

    private sealed class FakeTripBookingRepository(
        SharedStore store,
        Func<string, CancellationToken, Task> lockAcquirer) : ITripBookingRepository
    {
        public Task<IReadOnlyList<Guid>> GetExpiredUnpaidHoldOrderIdsAsync(DateTimeOffset utcNow, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AcquireIdempotencyLockAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default)
            => lockAcquirer($"trip-booking:idem:{customerUserId:N}:{idempotencyKey}", cancellationToken);

        public Task AcquireBookingLockAsync(Guid orderId, CancellationToken cancellationToken = default)
            => lockAcquirer($"trip-booking:order:{orderId:N}", cancellationToken);

        public Task<Account?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Accounts.SingleOrDefault(a => a.UserId == userId));

        public Task<IReadOnlyList<Account>> GetConfirmedAccountsByEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Account>>(store.Accounts
                .Where(a => a.EmailConfirmedAt is not null && normalizedEmails.Contains(a.Email.ToLowerInvariant()))
                .ToList());

        public Task<IReadOnlyList<BookingParticipant>> GetUnlinkedPaidParticipantsByEmailAsync(string normalizedEmail, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Trips.SingleOrDefault(t => t.TripId == tripId && !t.IsDeleted));

        public Task<int> GetReservedParticipantCountAsync(Guid tripId, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
        {
            var count = store.Orders
                .Where(o => !o.IsDeleted && o.TripBookingOrder?.TripId == tripId)
                .Where(o => o.Status == OrderStatuses.Confirmed || (o.Status == OrderStatuses.PendingPayment && o.HoldExpiresAt > utcNow))
                .Sum(o => o.TripBookingOrder.ParticipantCount);
            return Task.FromResult(count);
        }

        public Task<Order?> GetByIdempotencyKeyAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Orders.SingleOrDefault(o => o.CustomerUserId == customerUserId && o.IdempotencyKey == idempotencyKey));

        public Task<Order?> GetForAccessAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
        {
            foreach (var trip in store.Trips)
            {
                foreach (var booking in trip.TripBookingOrders)
                {
                    foreach (var p in booking.Participants)
                    {
                        if (p.TripGroupId.HasValue)
                            p.TripGroup = trip.TripGroups.FirstOrDefault(g => g.TripGroupId == p.TripGroupId.Value);
                    }
                }
            }

            var order = store.Orders.SingleOrDefault(o =>
                o.OrderId == orderId &&
                (o.CustomerUserId == userId ||
                 o.TripBookingOrder.Participants.Any(p =>
                     p.LinkedUserId == userId &&
                     p.LinkedUser is not null &&
                     p.LinkedUser.EmailConfirmedAt is not null &&
                     string.Equals(p.LinkedUser.Status, "Active", StringComparison.OrdinalIgnoreCase))));
            return Task.FromResult(order);
        }

        public Task<Order?> GetOwnedBookingAsync(Guid orderId, Guid ownerUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Orders.SingleOrDefault(o => o.OrderId == orderId && o.CustomerUserId == ownerUserId));

        public Task<decimal> GetSucceededPaymentTotalAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Orders.Single(o => o.OrderId == orderId).TotalAmount);

        public Task<Order?> GetBookingAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Orders.SingleOrDefault(o => o.OrderId == orderId));

        public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            foreach (var participant in order.TripBookingOrder.Participants)
            {
                participant.BookingOrder = order.TripBookingOrder;
                participant.Trip = order.TripBookingOrder.Trip;
            }
            order.TripBookingOrder.Trip.TripBookingOrders.Add(order.TripBookingOrder);
            store.Orders.Add(order);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSystemConfigurationRepository(SharedStore store) : ISystemConfigurationRepository
    {
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(key == TripGroupConfigKeys.GroupOverflowSlots ? store.OverflowSlots.ToString(CultureInfo.InvariantCulture) : null);

        public Task<SystemConfiguration?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<SystemConfiguration>> GetActiveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemConfiguration?> GetForUpdateAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(SystemConfiguration entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<SystemConfiguration> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(SystemConfiguration entity) => throw new NotSupportedException();
        public void Remove(SystemConfiguration entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<SystemConfiguration> entities) => throw new NotSupportedException();
    }

    private sealed class FakeListRepository<TEntity>(List<TEntity> list)
        : ITourRecurrenceRuleRepository, ITripRepository
        where TEntity : class
    {
        Task<IReadOnlyList<TourRecurrenceRule>> ITourRecurrenceRuleRepository.GetByTourIdAsync(Guid tourId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<RefundPolicy?> ITourRecurrenceRuleRepository.GetDefaultOrSpecificRefundPolicyAsync(
                Guid? refundPolicyId,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<TourRecurrenceRule?> ITourRecurrenceRuleRepository.GetRuleForUpdateAsync(Guid ruleId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<TourRecurrenceRule?> ITourRecurrenceRuleRepository.GetRuleWithTourAsync(Guid ruleId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Tour?> ITourRecurrenceRuleRepository.GetTourByIdAsync(Guid tourId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<Trip>> ITourRecurrenceRuleRepository.GetTripsByTourAndRangeAsync(
                Guid tourId,
                DateTimeOffset fromUtc,
                DateTimeOffset toUtc,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task ITripRepository.AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task ITripRepository.AddPaymentsAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task ITripRepository.AddTripProvidedItemsAsync(IEnumerable<TripProvidedItem> items, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Account?> ITripRepository.GetAccountByIdAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<BookingParticipant>> ITripRepository.GetActiveParticipantsByTripAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<RefundPolicy?> ITripRepository.GetActiveRefundPolicyAsync(Guid refundPolicyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripGroup>> ITripRepository.GetActiveTripGroupsAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripGroup>> ITripRepository.GetActiveTripGroupsForUpdateAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Trip?> ITripRepository.GetAdminTripDetailAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<(IReadOnlyList<Trip> Items, int TotalCount)> ITripRepository.GetPagedAdminTripsAsync(
                Guid? tourId,
                string? status,
                DateTimeOffset? fromStartAt,
                DateTimeOffset? toStartAt,
                Guid? managedByUserId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Dictionary<Guid, int>> ITripRepository.GetSoldSlotsByTripIdsAsync(
                IReadOnlyCollection<Guid> tripIds,
                DateTimeOffset utcNow,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<int> ITripRepository.GetSoldSlotsCountAsync(Guid tripId, DateTimeOffset utcNow, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<Payment>> ITripRepository.GetSucceededPaymentsByOrderIdsAsync(
                IReadOnlyCollection<Guid> orderIds,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Tour?> ITripRepository.GetTourWithProvidedItemsAsync(Guid tourId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripBookingOrder>> ITripRepository.GetTripBookingsForUpdateAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripBookingOrder>> ITripRepository.GetTripBookingsWithDetailsAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Trip?> ITripRepository.GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripProvidedItem>> ITripRepository.GetTripProvidedItemsForUpdateAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripProvidedItem>> ITripRepository.GetTripProvidedItemsWithAssetsAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Trip?> ITripRepository.GetTripWithTourAndRequirementsAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();

        Task<TourRecurrenceRule?> IGenericRepository<TourRecurrenceRule>.GetByIdAsync(object id, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task IGenericRepository<TourRecurrenceRule>.AddAsync(TourRecurrenceRule entity, CancellationToken cancellationToken)
        {
            ((List<TourRecurrenceRule>)(object)list).Add(entity);
            return Task.CompletedTask;
        }
        Task IGenericRepository<TourRecurrenceRule>.AddRangeAsync(IEnumerable<TourRecurrenceRule> entities, CancellationToken cancellationToken)
        {
            ((List<TourRecurrenceRule>)(object)list).AddRange(entities);
            return Task.CompletedTask;
        }
        void IGenericRepository<TourRecurrenceRule>.Update(TourRecurrenceRule entity) { }
        void IGenericRepository<TourRecurrenceRule>.Remove(TourRecurrenceRule entity) => ((List<TourRecurrenceRule>)(object)list).Remove(entity);
        void IGenericRepository<TourRecurrenceRule>.RemoveRange(IEnumerable<TourRecurrenceRule> entities) { }

        Task<Trip?> IGenericRepository<Trip>.GetByIdAsync(object id, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task IGenericRepository<Trip>.AddAsync(Trip entity, CancellationToken cancellationToken)
        {
            ((List<Trip>)(object)list).Add(entity);
            return Task.CompletedTask;
        }
        Task IGenericRepository<Trip>.AddRangeAsync(IEnumerable<Trip> entities, CancellationToken cancellationToken)
        {
            ((List<Trip>)(object)list).AddRange(entities);
            return Task.CompletedTask;
        }
        void IGenericRepository<Trip>.Update(Trip entity) { }
        void IGenericRepository<Trip>.Remove(Trip entity) => ((List<Trip>)(object)list).Remove(entity);
        void IGenericRepository<Trip>.RemoveRange(IEnumerable<Trip> entities) { }
    }

    private sealed class FakeTripProvidedItemRepository(List<TripProvidedItem> items) : ITripProvidedItemRepository
    {
        Task<List<EquipmentAsset>> ITripProvidedItemRepository.GetAvailableAssetsForItemAsync(Guid productVariantId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<TripProvidedItem?> ITripProvidedItemRepository.GetByIdWithDetailsAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<List<TripProvidedItem>> ITripProvidedItemRepository.GetByTripIdWithDetailsAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddAsync(TripProvidedItem entity, CancellationToken cancellationToken = default)
        {
            items.Add(entity);
            return Task.CompletedTask;
        }
        public Task<TripProvidedItem?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<TripProvidedItem> entities, CancellationToken cancellationToken = default) { items.AddRange(entities); return Task.CompletedTask; }
        public void Update(TripProvidedItem entity) { }
        public void Remove(TripProvidedItem entity) => items.Remove(entity);
        public void RemoveRange(IEnumerable<TripProvidedItem> entities) { }
    }

    private sealed class FakeAuditLogRepository(List<AuditLog> logs) : IAuditLogRepository
    {
        public Task AddAsync(AuditLog entity, CancellationToken cancellationToken = default)
        {
            lock (logs)
            {
                logs.Add(entity);
            }
            return Task.CompletedTask;
        }

        public Task<AuditLog?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<AuditLogRow> Items, int TotalCount)> GetPagedAsync(AuditLogFilter filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AuditLogRow>> GetForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountAsync(string action, Guid entityId, CancellationToken cancellationToken = default) { lock (logs) return Task.FromResult(logs.Count(x => x.Action == action && x.EntityId == entityId)); }
        public Task AddRangeAsync(IEnumerable<AuditLog> entities, CancellationToken cancellationToken = default) { lock (logs) logs.AddRange(entities); return Task.CompletedTask; }
        public void Update(AuditLog entity) { }
        public void Remove(AuditLog entity) { lock (logs) logs.Remove(entity); }
        public void RemoveRange(IEnumerable<AuditLog> entities) { }
    }

    private sealed class FakeNotificationRepository(List<Notification> notifications) : INotificationRepository
    {
        public Task AddAsync(Notification entity, CancellationToken cancellationToken = default)
        {
            notifications.Add(entity);
            return Task.CompletedTask;
        }
        public Task<Notification?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Notification> entities, CancellationToken cancellationToken = default) { notifications.AddRange(entities); return Task.CompletedTask; }
        public void Update(Notification entity) { }
        public void Remove(Notification entity) => notifications.Remove(entity);
        public void RemoveRange(IEnumerable<Notification> entities) { }

        public Task<bool> ExistsByEventKeyAsync(Guid recipientUserId, string eventKey, CancellationToken cancellationToken = default)
            => Task.FromResult(notifications.Any(n => n.RecipientUserId == recipientUserId && n.EventKey == eventKey));

        public Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedForUserAsync(
            Guid recipientUserId, bool? unreadOnly, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = notifications.Where(n => n.RecipientUserId == recipientUserId && (unreadOnly != true || n.ReadAt is null));
            var items = query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyList<Notification>, int)>((items, query.Count()));
        }

        public Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(notifications.Count(n => n.RecipientUserId == recipientUserId && n.ReadAt is null));

        public Task<Notification?> GetOwnedForUpdateAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(notifications.SingleOrDefault(n => n.NotificationId == notificationId && n.RecipientUserId == recipientUserId));

        public Task<IReadOnlyList<Notification>> GetUnreadOwnedForUpdateAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Notification>>(notifications.Where(n => n.RecipientUserId == recipientUserId && n.ReadAt is null).ToList());
    }
}
