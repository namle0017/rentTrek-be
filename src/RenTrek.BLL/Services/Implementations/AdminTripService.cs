using RenTrek.BLL.Constants;
using System.Text;
using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.AdminTrip.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ quản lý chuyến đi và điều hành khởi hành cho Admin/Staff (Màn O09, O10, O11).
/// </summary>
public sealed class AdminTripService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IAdminTripService
{
    public async Task<PagedResponse<AdminTripItemResponse>> GetPagedAsync(
        AdminTripQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 200 ? 10 : request.PageSize;
        var utcNow = timeProvider.GetUtcNow();

        var (trips, totalCount) = await unitOfWork.Trips.GetPagedAdminTripsAsync(
            request.TourId,
            request.Status,
            request.FromStartAt,
            request.ToStartAt,
            request.ManagedByUserId,
            pageNumber,
            pageSize,
            cancellationToken);

        var tripIds = trips.Select(x => x.TripId).ToArray();
        var soldSlotsMap = await unitOfWork.Trips.GetSoldSlotsByTripIdsAsync(tripIds, utcNow, cancellationToken);

        var items = trips.Select(trip =>
        {
            var soldSlots = soldSlotsMap.GetValueOrDefault(trip.TripId, 0);
            return new AdminTripItemResponse
            {
                TripId = trip.TripId,
                TourId = trip.TourId,
                TourName = trip.Tour?.Name ?? string.Empty,
                TourVersion = trip.Tour?.Version ?? 1,
                StartAt = trip.StartAt,
                EndAt = trip.EndAt,
                TotalSlots = trip.TotalSlots,
                SoldSlots = soldSlots,
                MinParticipants = trip.MinParticipants,
                PricePerPerson = trip.PricePerPerson,
                ManagedByUserId = trip.ManagedByUserId,
                ManagerFullName = trip.ManagedByUser?.FullName,
                Source = trip.RecurrenceRuleId.HasValue ? "RecurrenceRule" : "Manual",
                Status = trip.Status
            };
        }).ToList();

        return new PagedResponse<AdminTripItemResponse>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalCount
        };
    }

    public async Task<AdminTripDetailResponse> CreateAsync(
        CreateAdminTripRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EndAt <= request.StartAt)
            throw new BadRequestException("Thời gian kết thúc phải sau thời gian khởi hành", "VALIDATION_ERROR");

        if (request.TotalSlots < 1 || request.MinParticipants < 1 || request.MinParticipants > request.TotalSlots)
            throw new BadRequestException("Số khách tối thiểu phải từ 1 đến tổng sức chứa", "VALIDATION_ERROR");

        if (request.PricePerPerson < 0)
            throw new BadRequestException("Giá mỗi người không được âm", "VALIDATION_ERROR");

        var tour = await unitOfWork.Trips.GetTourWithProvidedItemsAsync(request.TourId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tour");

        if (!IsTourPublishable(tour.Status))
            throw new ConflictException("Tour chưa được xuất bản hoặc đã ngừng hoạt động", "TOUR_NOT_PUBLISHED");

        var refundPolicy = await unitOfWork.Trips.GetActiveRefundPolicyAsync(request.RefundPolicyId, cancellationToken)
            ?? throw new ConflictException("Chính sách hoàn tiền không tồn tại hoặc không hoạt động", "REFUND_POLICY_INACTIVE");

        EnsureStaffAssignsSelf(request.ManagedByUserId);
        var managerUserId = request.ManagedByUserId
                            ?? currentUser.UserId
                            ?? tour.CreatedByUserId
                            ?? refundPolicy.CreatedByUserId;
        Account? manager = null;
        if (managerUserId != Guid.Empty)
        {
            manager = await unitOfWork.Trips.GetAccountByIdAsync(managerUserId, cancellationToken);
            if (request.ManagedByUserId.HasValue && request.ManagedByUserId.Value != Guid.Empty && manager is null)
                throw new NotFoundException("Không tìm thấy người điều phối chuyến đi");
        }

        var utcNow = timeProvider.GetUtcNow();
        var tripId = Guid.NewGuid();
        var trip = new Trip
        {
            TripId = tripId,
            TourId = tour.Id,
            RefundPolicyId = refundPolicy.RefundPolicyId,
            ManagedByUserId = manager?.UserId ?? managerUserId,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            TotalSlots = request.TotalSlots,
            MinParticipants = request.MinParticipants,
            PricePerPerson = request.PricePerPerson,
            Status = TripStatuses.Draft,
            RowVersion = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await unitOfWork.Trips.AddAsync(trip, cancellationToken);

        var providedItems = tour.ProvidedItems
            .Where(p => !p.IsDeleted && p.ProductVariantId.HasValue)
            .Select(p => new TripProvidedItem
            {
                TripProvidedItemId = Guid.NewGuid(),
                TripId = tripId,
                TourProvidedItemId = p.Id,
                ProductVariantId = p.ProductVariantId!.Value,
                RequiredQuantity = Math.Max(
                    1,
                    string.Equals(p.QuantityBasis, "PerPerson", StringComparison.OrdinalIgnoreCase)
                        ? p.Quantity * request.TotalSlots
                        : p.Quantity),
                Status = TripProvidedItemStatuses.Missing,
                CreatedAt = utcNow.UtcDateTime
            })
            .ToList();

        if (providedItems.Count > 0)
            await unitOfWork.Trips.AddTripProvidedItemsAsync(providedItems, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        trip.Tour = tour;
        trip.RefundPolicy = refundPolicy;
        trip.ManagedByUser = manager!;

        return MapDetailResponse(trip, 0);
    }

    public async Task<AdminTripDetailResponse> GetByIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetAdminTripDetailAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var soldSlots = await unitOfWork.Trips.GetSoldSlotsCountAsync(
            tripId,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return MapDetailResponse(trip, soldSlots);
    }

    public async Task<AdminTripDetailResponse> UpdateAsync(
        Guid tripId,
        UpdateAdminTripRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EndAt <= request.StartAt)
            throw new BadRequestException("Thời gian kết thúc phải sau thời gian khởi hành", "VALIDATION_ERROR");

        if (request.TotalSlots < 1 || request.MinParticipants < 1 || request.MinParticipants > request.TotalSlots)
            throw new BadRequestException("Số khách tối thiểu phải từ 1 đến tổng sức chứa", "VALIDATION_ERROR");

        if (request.PricePerPerson < 0)
            throw new BadRequestException("Giá mỗi người không được âm", "VALIDATION_ERROR");

        var trip = await unitOfWork.Trips.GetTripForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");
        TripGroupRules.EnsureManagesTrip(currentUser, trip);

        if (request.RowVersion > 0 && trip.RowVersion != request.RowVersion)
        {
            throw new ConflictException(
                "Dữ liệu chuyến đi đã bị thay đổi bởi người khác. Vui lòng tải lại trang.",
                "CONCURRENCY_CONFLICT");
        }

        var utcNow = timeProvider.GetUtcNow();
        var soldSlots = await unitOfWork.Trips.GetSoldSlotsCountAsync(tripId, utcNow, cancellationToken);
        if (request.TotalSlots < soldSlots)
        {
            throw new ConflictException(
                $"Không được giảm tổng số chỗ ({request.TotalSlots}) thấp hơn số chỗ đã cam kết ({soldSlots})",
                "TOTAL_SLOTS_BELOW_SOLD");
        }

        if (request.RefundPolicyId.HasValue && request.RefundPolicyId.Value != Guid.Empty && request.RefundPolicyId.Value != trip.RefundPolicyId)
        {
            var refundPolicy = await unitOfWork.Trips.GetActiveRefundPolicyAsync(request.RefundPolicyId.Value, cancellationToken)
                ?? throw new ConflictException("Chính sách hoàn tiền không tồn tại hoặc không hoạt động", "REFUND_POLICY_INACTIVE");
            trip.RefundPolicyId = refundPolicy.RefundPolicyId;
            trip.RefundPolicy = refundPolicy;
        }

        EnsureStaffAssignsSelf(request.ManagedByUserId);
        if (request.ManagedByUserId.HasValue && request.ManagedByUserId.Value != Guid.Empty)
        {
            var manager = await unitOfWork.Trips.GetAccountByIdAsync(request.ManagedByUserId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy người điều phối chuyến đi");
            trip.ManagedByUserId = manager.UserId;
            trip.ManagedByUser = manager;
        }

        trip.StartAt = request.StartAt;
        trip.EndAt = request.EndAt;
        trip.TotalSlots = request.TotalSlots;
        trip.MinParticipants = request.MinParticipants;
        trip.PricePerPerson = request.PricePerPerson;
        trip.RowVersion++;
        trip.UpdatedAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapDetailResponse(trip, soldSlots);
    }

    public async Task<TripReadinessChecklistResponse> GetReadinessChecklistAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetAdminTripDetailAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var utcNow = timeProvider.GetUtcNow();
        var soldSlots = await unitOfWork.Trips.GetSoldSlotsCountAsync(tripId, utcNow, cancellationToken);
        var participants = await unitOfWork.Trips.GetActiveParticipantsByTripAsync(tripId, cancellationToken);
        var groups = await unitOfWork.Trips.GetActiveTripGroupsAsync(tripId, cancellationToken);
        var providedItems = await unitOfWork.Trips.GetTripProvidedItemsWithAssetsAsync(tripId, cancellationToken);
        var bookings = await unitOfWork.Trips.GetTripBookingsWithDetailsAsync(tripId, cancellationToken);

        var activeParticipants = participants
            .Where(p => p.BookingOrder.Order.Status != OrderStatuses.Cancelled && p.BookingOrder.Order.Status != OrderStatuses.Expired)
            .ToList();

        var unassignedCount = activeParticipants.Count(p => p.TripGroupId is null);
        var totalGroups = groups.Count;
        var assignedGroups = groups.Count(g => g.GuideUserId != Guid.Empty);

        var missingItemsCount = providedItems.Count(item =>
        {
            var allocatedCount = item.AssetAssignments.Count(a =>
                a.Status == AssignmentStatuses.Reserved || a.Status == AssignmentStatuses.Issued || a.Status == AssignmentStatuses.Returned);
            return !string.Equals(item.Status, TripProvidedItemStatuses.Ready, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(item.Status, TripProvidedItemStatuses.Recovered, StringComparison.OrdinalIgnoreCase) &&
                   allocatedCount < item.RequiredQuantity;
        });

        var activeOrders = bookings
            .Where(b => b.Order.Status != OrderStatuses.Cancelled && b.Order.Status != OrderStatuses.Expired)
            .ToList();
        var unpaidOrdersCount = activeOrders.Count(b =>
            !string.Equals(b.Order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase));

        var minParticipantsPassed = soldSlots >= trip.MinParticipants;
        var allGroupedPassed = activeParticipants.Count > 0 && unassignedCount == 0;
        var allGuidesPassed = totalGroups > 0 && assignedGroups == totalGroups;
        var allItemsPassed = missingItemsCount == 0;
        var allOrdersPaidPassed = activeOrders.Count > 0 && unpaidOrdersCount == 0;

        var isReady = minParticipantsPassed
                      && allGroupedPassed
                      && allGuidesPassed
                      && allItemsPassed
                      && allOrdersPaidPassed;

        return new TripReadinessChecklistResponse
        {
            IsReadyToDepart = isReady,
            Checklist = new TripReadinessChecklistDetailResponse
            {
                MinParticipantsMet = new ReadinessMinParticipantsItemResponse
                {
                    Passed = minParticipantsPassed,
                    Current = soldSlots,
                    Required = trip.MinParticipants
                },
                AllParticipantsGrouped = new ReadinessParticipantsGroupedItemResponse
                {
                    Passed = allGroupedPassed,
                    UnassignedCount = unassignedCount
                },
                AllGroupsHaveGuides = new ReadinessGroupsGuidedItemResponse
                {
                    Passed = allGuidesPassed,
                    AssignedGroups = assignedGroups,
                    TotalGroups = totalGroups
                },
                AllProvidedItemsReady = new ReadinessProvidedItemsItemResponse
                {
                    Passed = allItemsPassed,
                    MissingItemsCount = missingItemsCount
                },
                AllOrdersPaid = new ReadinessOrdersPaidItemResponse
                {
                    Passed = allOrdersPaidPassed,
                    UnpaidOrdersCount = unpaidOrdersCount
                }
            }
        };
    }

    public async Task<AdminTripDetailResponse> OpenBookingAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetTripForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");
        TripGroupRules.EnsureManagesTrip(currentUser, trip);

        if (!string.Equals(trip.Status, TripStatuses.Draft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(trip.Status, TripStatuses.ClosedForBooking, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"Không thể mở nhận khách cho chuyến đi đang ở trạng thái {trip.Status}",
                "INVALID_TRIP_STATUS_TRANSITION");
        }

        var utcNow = timeProvider.GetUtcNow();
        trip.Status = TripStatuses.Open;
        trip.RowVersion++;
        trip.UpdatedAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var soldSlots = await unitOfWork.Trips.GetSoldSlotsCountAsync(tripId, utcNow, cancellationToken);
        return MapDetailResponse(trip, soldSlots);
    }

    public async Task<AdminTripDetailResponse> CloseBookingAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetTripForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");
        TripGroupRules.EnsureManagesTrip(currentUser, trip);

        if (!string.Equals(trip.Status, TripStatuses.Open, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"Chỉ chuyến đi đang ở trạng thái Open mới có thể đóng nhận khách (hiện tại: {trip.Status})",
                "INVALID_TRIP_STATUS_TRANSITION");
        }

        var utcNow = timeProvider.GetUtcNow();
        trip.Status = TripStatuses.ClosedForBooking;
        trip.RowVersion++;
        trip.UpdatedAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var soldSlots = await unitOfWork.Trips.GetSoldSlotsCountAsync(tripId, utcNow, cancellationToken);
        return MapDetailResponse(trip, soldSlots);
    }

    public async Task<AdminTripDetailResponse> StartTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetTripForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");
        TripGroupRules.EnsureManagesTrip(currentUser, trip);

        if (!string.Equals(trip.Status, TripStatuses.ClosedForBooking, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"Chỉ chuyến đi ở trạng thái ClosedForBooking mới có thể xuất phát (hiện tại: {trip.Status})",
                "INVALID_TRIP_STATUS_TRANSITION");
        }

        var readiness = await GetReadinessChecklistAsync(tripId, cancellationToken);
        if (!readiness.IsReadyToDepart)
        {
            throw new ConflictException(
                "Chuyến đi chưa đạt đủ 5 điều kiện an toàn khởi hành (Readiness Checklist)",
                "TRIP_NOT_READY_TO_DEPART");
        }

        var utcNow = timeProvider.GetUtcNow();
        trip.Status = TripStatuses.InProgress;
        trip.RowVersion++;
        trip.UpdatedAt = utcNow;

        var groups = await unitOfWork.Trips.GetActiveTripGroupsForUpdateAsync(tripId, cancellationToken);
        foreach (var group in groups)
        {
            group.Status = TripGroupStatuses.InProgress;
            group.UpdatedAt = utcNow.UtcDateTime;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var soldSlots = await unitOfWork.Trips.GetSoldSlotsCountAsync(tripId, utcNow, cancellationToken);
        return MapDetailResponse(trip, soldSlots);
    }

    public async Task<AdminTripDetailResponse> CompleteTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetTripForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");
        TripGroupRules.EnsureManagesTrip(currentUser, trip);

        if (!string.Equals(trip.Status, TripStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"Chỉ chuyến đi đang InProgress mới có thể hoàn tất (hiện tại: {trip.Status})",
                "INVALID_TRIP_STATUS_TRANSITION");
        }

        var utcNow = timeProvider.GetUtcNow();
        trip.Status = TripStatuses.Completed;
        trip.RowVersion++;
        trip.UpdatedAt = utcNow;

        var groups = await unitOfWork.Trips.GetActiveTripGroupsForUpdateAsync(tripId, cancellationToken);
        foreach (var group in groups)
        {
            group.Status = TripGroupStatuses.Completed;
            group.UpdatedAt = utcNow.UtcDateTime;
        }

        var bookings = await unitOfWork.Trips.GetTripBookingsForUpdateAsync(tripId, cancellationToken);
        foreach (var booking in bookings.Where(b => b.Order.Status == OrderStatuses.Confirmed))
        {
            booking.Order.Status = OrderStatuses.Completed;
            booking.Order.UpdatedAt = utcNow;
            foreach (var participant in booking.Participants.Where(p => p.Status != BookingParticipantStatuses.Cancelled))
            {
                participant.Status = BookingParticipantStatuses.Completed;
                participant.UpdatedAt = utcNow;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var soldSlots = await unitOfWork.Trips.GetSoldSlotsCountAsync(tripId, utcNow, cancellationToken);
        return MapDetailResponse(trip, soldSlots);
    }

    public async Task<CancelAdminTripResponse> CancelTripAsync(
        Guid tripId,
        CancelAdminTripRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BadRequestException("Bắt buộc nhập lý do hủy chuyến", "VALIDATION_ERROR");

        var reason = request.Reason.Trim();
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var trip = await unitOfWork.Trips.GetTripForUpdateAsync(tripId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi", "NOT_FOUND");

            if (string.Equals(trip.Status, TripStatuses.Completed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trip.Status, TripStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Chuyến đi đã kết thúc hoặc đã bị hủy trước đó.", "TRIP_NOT_CANCELLABLE");
            }

            trip.Status = TripStatuses.Cancelled;
            trip.RowVersion++;
            trip.UpdatedAt = utcNow;

            var bookings = await unitOfWork.Trips.GetTripBookingsForUpdateAsync(tripId, cancellationToken);
            var activeBookings = bookings
                .Where(b => b.Order.Status != OrderStatuses.Cancelled && b.Order.Status != OrderStatuses.Expired)
                .ToList();

            var orderIds = activeBookings.Select(b => b.OrderId).ToArray();
            var succeededPayments = await unitOfWork.Trips.GetSucceededPaymentsByOrderIdsAsync(orderIds, cancellationToken);
            var paymentsByOrder = succeededPayments
                .GroupBy(p => p.OrderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var refundPayments = new List<Payment>();
            var affectedParticipantsCount = 0;
            var totalRefundAmount = 0m;

            foreach (var booking in activeBookings)
            {
                booking.Order.Status = OrderStatuses.Cancelled;
                booking.Order.CancelledAt = utcNow;
                booking.Order.CancelReason = reason;
                booking.Order.UpdatedAt = utcNow;

                foreach (var participant in booking.Participants.Where(p => p.Status != BookingParticipantStatuses.Cancelled))
                {
                    participant.Status = BookingParticipantStatuses.Cancelled;
                    participant.UpdatedAt = utcNow;
                    affectedParticipantsCount++;
                }

                var orderPayments = paymentsByOrder.GetValueOrDefault(booking.OrderId);
                var paidAmount = orderPayments?.Sum(p => p.Amount) ?? 0m;
                if (paidAmount <= 0m && string.Equals(booking.Order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                {
                    paidAmount = booking.Order.TotalAmount;
                }

                if (paidAmount > 0m)
                {
                    var originalPayment = orderPayments?.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
                    refundPayments.Add(new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        OrderId = booking.OrderId,
                        OriginalPaymentId = originalPayment?.PaymentId,
                        PaymentType = "Refund",
                        Amount = paidAmount,
                        PaymentMethod = originalPayment?.PaymentMethod ?? "BankTransfer",
                        Status = PaymentStatuses.PendingApproval,
                        Purpose = $"Hoàn tiền 100% do hủy chuyến: {reason}",
                        CreatedAt = utcNow
                    });
                    totalRefundAmount += paidAmount;
                }
            }

            if (refundPayments.Count > 0)
                await unitOfWork.Trips.AddPaymentsAsync(refundPayments, cancellationToken);

            var groups = await unitOfWork.Trips.GetActiveTripGroupsForUpdateAsync(tripId, cancellationToken);
            foreach (var group in groups)
            {
                group.Status = TripGroupStatuses.Cancelled;
                group.UpdatedAt = utcNow.UtcDateTime;
            }

            var providedItems = await unitOfWork.Trips.GetTripProvidedItemsForUpdateAsync(tripId, cancellationToken);
            var releasedAssetsCount = 0;
            foreach (var item in providedItems)
            {
                item.Status = TripProvidedItemStatuses.Cancelled;
                foreach (var assignment in item.AssetAssignments.Where(a => a.Status == AssignmentStatuses.Reserved))
                {
                    assignment.Status = AssignmentStatuses.Cancelled;
                    releasedAssetsCount++;
                }
            }

            await unitOfWork.Trips.AddAuditLogAsync(new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                ActorUserId = currentUser.UserId,
                Action = "CANCEL_TRIP",
                EntityType = "Trip",
                EntityId = trip.TripId,
                Details = $"Hủy chuyến đi {trip.TripId}. Lý do: {reason}. Đơn bị ảnh hưởng: {activeBookings.Count}, khách: {affectedParticipantsCount}, tổng hoàn tiền: {totalRefundAmount:N0}, thiết bị giải phóng: {releasedAssetsCount}.",
                CreatedAt = utcNow.UtcDateTime
            }, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return new CancelAdminTripResponse
            {
                TripId = trip.TripId,
                AffectedBookingsCount = activeBookings.Count,
                AffectedParticipantsCount = affectedParticipantsCount,
                TotalRefundAmount = totalRefundAmount,
                ReleasedAssetsCount = releasedAssetsCount
            };
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<AdminTripBookingItemResponse>> GetTripBookingsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        _ = await unitOfWork.Trips.GetAdminTripDetailAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var bookings = await unitOfWork.Trips.GetTripBookingsWithDetailsAsync(tripId, cancellationToken);

        return bookings.Select(b => new AdminTripBookingItemResponse
        {
            OrderId = b.OrderId,
            OrderCode = b.Order.OrderCode,
            CustomerUserId = b.Order.CustomerUserId,
            CustomerName = b.Order.CustomerUser?.FullName ?? string.Empty,
            CustomerEmail = b.Order.CustomerUser?.Email ?? string.Empty,
            CustomerPhone = b.Order.CustomerUser?.Phone,
            ParticipantCount = b.ParticipantCount,
            UnitPrice = b.UnitPrice,
            TotalAmount = b.Order.TotalAmount,
            OrderStatus = b.Order.Status,
            PaymentStatus = b.Order.PaymentStatus,
            GroupingPreference = FormatGroupingPreference(b.GroupingPreference),
            IsSplit = b.IsSplit,
            SplitReason = b.SplitReason,
            CreatedAt = b.Order.CreatedAt,
            Participants = b.Participants
                .OrderBy(p => p.FullName)
                .Select(p => new AdminTripParticipantItemResponse
                {
                    ParticipantId = p.ParticipantId,
                    FullName = p.FullName,
                    Email = p.Email,
                    Phone = p.Phone,
                    DateOfBirth = p.DateOfBirth,
                    EmergencyContactName = p.EmergencyContactName,
                    EmergencyContactPhone = p.EmergencyContactPhone,
                    Status = p.Status,
                    TripGroupId = p.TripGroupId,
                    TripGroupName = p.TripGroup?.GroupName
                }).ToList()
        }).ToList();
    }

    public async Task<TripManifestResponse> GetManifestAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetAdminTripDetailAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi");

        var participants = await unitOfWork.Trips.GetActiveParticipantsByTripAsync(tripId, cancellationToken);
        var manifestItems = participants
            .Where(p => p.BookingOrder.Order.Status != OrderStatuses.Cancelled && p.BookingOrder.Order.Status != OrderStatuses.Expired)
            .OrderBy(p => p.TripGroup?.GroupName ?? "ZZZZ")
            .ThenBy(p => p.FullName)
            .Select(p => new TripManifestParticipantItemResponse
            {
                ParticipantId = p.ParticipantId,
                FullName = p.FullName,
                Email = p.Email,
                Phone = p.Phone,
                DateOfBirth = p.DateOfBirth,
                EmergencyContactName = p.EmergencyContactName,
                EmergencyContactPhone = p.EmergencyContactPhone,
                OrderCode = p.BookingOrder.Order.OrderCode,
                GroupName = p.TripGroup?.GroupName ?? "Chưa phân nhóm",
                GuideName = p.TripGroup?.GuideUser?.FullName
            })
            .ToList();

        return new TripManifestResponse
        {
            TripId = trip.TripId,
            TourName = trip.Tour?.Name ?? string.Empty,
            StartAt = trip.StartAt,
            EndAt = trip.EndAt,
            TotalParticipants = manifestItems.Count,
            Participants = manifestItems
        };
    }

    public async Task<(byte[] FileBytes, string FileName)> ExportManifestCsvAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var manifest = await GetManifestAsync(tripId, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("STT,HoTen,Email,SoDienThoai,NgaySinh,MaDon,Nhom,HuongDanVien,LienHeKhanCap,SDTKhanCap");

        for (var i = 0; i < manifest.Participants.Count; i++)
        {
            var p = manifest.Participants[i];
            sb.AppendLine(string.Join(",",
                i + 1,
                EscapeCsv(p.FullName),
                EscapeCsv(p.Email),
                EscapeCsv(p.Phone),
                EscapeCsv(p.DateOfBirth?.ToString("yyyy-MM-dd")),
                EscapeCsv(p.OrderCode),
                EscapeCsv(p.GroupName),
                EscapeCsv(p.GuideName),
                EscapeCsv(p.EmergencyContactName),
                EscapeCsv(p.EmergencyContactPhone)));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        return (bytes, $"manifest-{tripId:N}.csv");
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string FormatGroupingPreference(string? raw)
        => string.Equals(raw, "KeepTogether", StringComparison.OrdinalIgnoreCase)
            ? "Đi chung nhóm với đơn"
            : (string.IsNullOrWhiteSpace(raw) ? "Đi chung nhóm với đơn" : raw);

    private static bool IsTourPublishable(string? status)
        => string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

    private static AdminTripDetailResponse MapDetailResponse(Trip trip, int soldSlots)
        => new()
        {
            TripId = trip.TripId,
            TourId = trip.TourId,
            TourName = trip.Tour?.Name ?? string.Empty,
            TourVersion = trip.Tour?.Version ?? 1,
            RecurrenceRuleId = trip.RecurrenceRuleId,
            RefundPolicyId = trip.RefundPolicyId,
            RefundPolicyName = trip.RefundPolicy?.Name ?? string.Empty,
            ManagedByUserId = trip.ManagedByUserId,
            ManagerFullName = trip.ManagedByUser?.FullName,
            StartAt = trip.StartAt,
            EndAt = trip.EndAt,
            TotalSlots = trip.TotalSlots,
            SoldSlots = soldSlots,
            MinParticipants = trip.MinParticipants,
            PricePerPerson = trip.PricePerPerson,
            Source = trip.RecurrenceRuleId.HasValue ? "RecurrenceRule" : "Manual",
            Status = trip.Status,
            RowVersion = trip.RowVersion,
            GroupsCount = trip.TripGroups?.Count(g => !g.IsDeleted) ?? 0,
            CreatedAt = trip.CreatedAt,
            UpdatedAt = trip.UpdatedAt
        };

    /// <summary>PRD §3.2 "G": Staff chỉ được để chính mình phụ trách chuyến; giao cho người khác là việc của Admin.</summary>
    private void EnsureStaffAssignsSelf(Guid? managedByUserId)
    {
        if (currentUser.Role == RoleNames.Staff && managedByUserId is { } id && id != Guid.Empty && id != currentUser.UserId)
            throw new ForbiddenException("Staff không được giao chuyến cho người khác phụ trách", ErrorCodes.TripNotAssigned);
    }
}
