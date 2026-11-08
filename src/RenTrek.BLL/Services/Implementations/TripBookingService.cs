using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ trip booking: đặt chuyến (kể cả đặt giùm), sửa người đi, và link xem lịch chuyến đi.
/// </summary>
public sealed class TripBookingService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IInviteTokenHelper inviteTokenHelper,
    IOptions<EmailSettings> emailOptions,
    INotificationService notificationService,
    IRefundCaseService? refundCaseService = null) : ITripBookingService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<TripBookingResponse> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequiredUserId;
        var order = await unitOfWork.TripBookings.GetForAccessAsync(orderId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy booking");

        if (order.CustomerUserId != userId)
        {
            var account = await unitOfWork.TripBookings.GetAccountAsync(userId, cancellationToken);
            if (account is null ||
                account.EmailConfirmedAt is null ||
                !string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase) ||
                !order.TripBookingOrder.Participants.Any(p => p.LinkedUserId == userId))
            {
                throw new NotFoundException("Không tìm thấy booking");
            }
        }

        var response = MapResponse(order, userId);
        if (response.CanManage && refundCaseService is not null)
            response.RefundCase = await refundCaseService.GetSummaryByOrderAsync(orderId, cancellationToken);
        return response;
    }

    public async Task<CancellationPreviewResponse> GetCancellationPreviewAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var refunds = refundCaseService ?? throw new InvalidOperationException("Refund service is not configured");
        var order = await unitOfWork.TripBookings.GetOwnedBookingAsync(orderId, currentUser.RequiredUserId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy booking", ErrorCodes.OrderNotFound);

        var utcNow = timeProvider.GetUtcNow();
        var quote = await refunds.QuoteAsync(RefundOrderInput.From(order), utcNow, cancellationToken);
        var blockedReason = GetCancelBlockedReason(order, utcNow);

        return new CancellationPreviewResponse
        {
            CanCancel = blockedReason is null,
            BlockedReason = blockedReason,
            RefundPolicy = quote.Policy is null
                ? null
                : new CancellationPreviewResponse.PolicyInfo
                {
                    RefundPolicyId = quote.Policy.RefundPolicyId,
                    Name = quote.Policy.Name,
                    Version = quote.Policy.Version,
                    Rules = quote.Policy.RefundPolicyRules
                        .OrderByDescending(rule => rule.MinHoursBefore)
                        .Select(rule => new CancellationPreviewResponse.RuleInfo
                        {
                            MinHoursBefore = rule.MinHoursBefore,
                            RefundPercentage = rule.RefundPercentage
                        })
                        .ToList()
                },
            AppliedCase = quote.AppliedCase,
            PaidAmount = quote.PaidAmount,
            EstimatedRefundAmount = quote.RefundAmount,
            ConsentVersion = RefundBankCodes.ConsentVersion
        };
    }

    public async Task<TripBookingResponse> CreateAsync(
        CreateTripBookingRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var contactPhone = ValidateCreateRequest(request);

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
            throw new BadRequestException("Idempotency-Key là bắt buộc và không vượt quá 100 ký tự");

        var userId = currentUser.RequiredUserId;
        var account = await unitOfWork.TripBookings.GetAccountAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản người đặt");
        if (!string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản không ở trạng thái hoạt động");

        var drafts = BuildParticipantDrafts(request, account, contactPhone);
        var normalizedKey = idempotencyKey.Trim();
        var existing = await unitOfWork.TripBookings.GetByIdempotencyKeyAsync(userId, normalizedKey, cancellationToken);
        if (existing is not null)
        {
            EnsureIdempotentRequestMatches(existing, request, drafts);
            return MapResponse(existing, userId);
        }

        var nonEmptyEmails = drafts.Where(d => d.Email is not null).Select(d => d.Email!).ToArray();
        if (nonEmptyEmails.Distinct(StringComparer.Ordinal).Count() != nonEmptyEmails.Length)
            throw new ConflictException("Email participant không được trùng trong cùng booking");

        var utcNow = timeProvider.GetUtcNow();
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripBookings.AcquireIdempotencyLockAsync(userId, normalizedKey, cancellationToken);
            existing = await unitOfWork.TripBookings.GetByIdempotencyKeyAsync(userId, normalizedKey, cancellationToken);
            if (existing is not null)
            {
                EnsureIdempotentRequestMatches(existing, request, drafts);
                await unitOfWork.CommitAsync(cancellationToken);
                return MapResponse(existing, userId);
            }

            var trip = await unitOfWork.TripBookings.GetTripForUpdateAsync(request.TripId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi");
            if (!string.Equals(trip.Status, TripStatuses.Open, StringComparison.OrdinalIgnoreCase) || trip.StartAt <= utcNow)
                throw new ConflictException("Chuyến đi không mở nhận booking");

            // Tour Archived/Expired chỉ còn để xem lại; trip Open của nó không nhận booking mới. Tour xoá mềm → null.
            var tour = await unitOfWork.Tours.GetByIdAsync(trip.TourId, cancellationToken);
            if (tour is null || tour.Status is not (TourStatuses.Active or TourStatuses.Published))
                throw new ConflictException("Tour đã ngừng nhận booking");

            var participantCount = drafts.Count;
            var reserved = await unitOfWork.TripBookings.GetReservedParticipantCountAsync(
                trip.TripId,
                utcNow,
                cancellationToken);
            if (reserved + participantCount > trip.TotalSlots)
                throw new ConflictException("Chuyến đi không còn đủ chỗ");

            var orderId = Guid.NewGuid();
            var subtotal = participantCount * trip.PricePerPerson;
            var booking = new TripBookingOrder
            {
                OrderId = orderId,
                TripId = trip.TripId,
                Trip = trip,
                ParticipantCount = participantCount,
                UnitPrice = trip.PricePerPerson,
                GroupingPreference = "KeepTogether",
                Participants = drafts.Select((draft, idx) => new BookingParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    BookingOrderId = orderId,
                    FullName = draft.FullName,
                    Email = draft.Email,
                    Phone = draft.Phone,
                    DateOfBirth = draft.DateOfBirth,
                    EmergencyContactName = draft.EmergencyContactName,
                    EmergencyContactPhone = draft.EmergencyContactPhone,
                    Status = BookingParticipantStatuses.Pending,
                    TripId = trip.TripId,
                    CreatedAt = utcNow.AddTicks(idx),
                    UpdatedAt = utcNow
                }).ToList()
            };

            var order = new Order
            {
                OrderId = orderId,
                CustomerUserId = account.UserId,
                OrderType = OrderTypes.TripBooking,
                TotalAmount = subtotal,
                SubtotalAmount = subtotal,
                DiscountAmount = 0m,
                Status = OrderStatuses.PendingPayment,
                PaymentStatus = OrderPaymentStatuses.Unpaid,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                OrderCode = $"RT-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..32].ToUpperInvariant(),
                Note = NormalizeOptionalText(request.Note),
                HoldExpiresAt = utcNow.AddMinutes(15),
                RefundPolicyId = trip.RefundPolicyId,
                IdempotencyKey = normalizedKey,
                TripBookingOrder = booking
            };
            booking.Order = order;

            await unitOfWork.TripBookings.AddOrderAsync(order, cancellationToken);
            var placedTourName = string.IsNullOrWhiteSpace(tour.Name) ? trip.Tour?.Name : tour.Name;
            var placedContent = string.IsNullOrWhiteSpace(placedTourName)
                ? $"Đơn {order.OrderCode} cho chuyến đi đã được tạo và đang chờ thanh toán."
                : $"Đơn {order.OrderCode} cho chuyến đi {placedTourName.Trim()} đang chờ thanh toán.";
            await notificationService.QueueAsync(
                order.CustomerUserId,
                NotificationTypes.OrderPlaced,
                "Đặt chuyến thành công",
                placedContent,
                "Order",
                order.OrderId,
                $"OrderPlaced:{order.OrderId}",
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapResponse(order, account.UserId);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TripBookingResponse> UpdateParticipantAsync(
        Guid orderId,
        Guid participantId,
        UpdateBookingParticipantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 200)
            throw new BadRequestException("Họ tên participant là bắt buộc và không vượt quá 200 ký tự");

        var newEmail = NormalizeOptionalEmail(request.Email);
        var newPhone = NormalizeOptionalText(request.Phone);
        if (newPhone?.Length > 20)
            throw new BadRequestException("Số điện thoại không vượt quá 20 ký tự");

        var utcNow = timeProvider.GetUtcNow();
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripBookings.AcquireBookingLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.TripBookings.GetOwnedBookingAsync(
                orderId,
                currentUser.RequiredUserId,
                cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy booking");

            if (string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
                throw new ConflictException("Đơn đã hủy, không thể chỉnh sửa");
            if (order.TripBookingOrder.Trip.StartAt <= utcNow)
                throw new ConflictException("Chuyến đi đã bắt đầu");

            var participant = order.TripBookingOrder.Participants.FirstOrDefault(x => x.ParticipantId == participantId)
                ?? throw new NotFoundException("Không tìm thấy người tham gia");

            if (newEmail is not null &&
                order.TripBookingOrder.Participants.Any(x =>
                    x.ParticipantId != participantId &&
                    string.Equals(NormalizeOptionalEmail(x.Email), newEmail, StringComparison.Ordinal)))
            {
                throw new ConflictException("Email participant không được trùng trong cùng booking");
            }

            var oldEmail = NormalizeOptionalEmail(participant.Email);
            var emailChanged = !string.Equals(oldEmail, newEmail, StringComparison.Ordinal);

            participant.FullName = request.FullName.Trim();
            participant.Email = newEmail;
            participant.Phone = newPhone;
            if (newPhone is null &&
                string.IsNullOrWhiteSpace(participant.EmergencyContactPhone) &&
                !string.IsNullOrWhiteSpace(order.CustomerUser?.Phone))
            {
                participant.EmergencyContactName = order.CustomerUser.FullName.Trim();
                participant.EmergencyContactPhone = order.CustomerUser.Phone.Trim();
            }

            if (emailChanged)
            {
                participant.LinkedUserId = null;
                if (newEmail is not null && IsPaid(order))
                {
                    var confirmed = await unitOfWork.TripBookings.GetConfirmedAccountsByEmailsAsync([newEmail], cancellationToken);
                    var matched = confirmed.FirstOrDefault(a =>
                        a.EmailConfirmedAt is not null &&
                        string.Equals(a.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(a.Email.Trim().ToLowerInvariant(), newEmail, StringComparison.Ordinal));
                    if (matched is not null)
                        participant.LinkedUserId = matched.UserId;
                }
            }

            participant.UpdatedAt = utcNow;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapResponse(order, order.CustomerUserId);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TripBookingResponse> CancelAsync(
        Guid orderId,
        CancelTripBookingRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var refunds = refundCaseService ?? throw new InvalidOperationException("Refund service is not configured");
        var actorUserId = currentUser.RequiredUserId;
        var isStaffOrAdmin = string.Equals(currentUser.Role, RoleNames.Staff, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(currentUser.Role, RoleNames.Admin, StringComparison.OrdinalIgnoreCase);
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.TripBookings.AcquireBookingLockAsync(orderId, cancellationToken);
            var order = isStaffOrAdmin
                ? await unitOfWork.TripBookings.GetBookingAsync(orderId, cancellationToken)
                : await unitOfWork.TripBookings.GetOwnedBookingAsync(orderId, actorUserId, cancellationToken);

            if (order?.TripBookingOrder is null || order.IsDeleted)
                throw new NotFoundException("Không tìm thấy booking", ErrorCodes.OrderNotFound);

            var blockedReason = GetCancelBlockedReason(order, utcNow);
            if (blockedReason is not null)
                throw new ConflictException(blockedReason, ErrorCodes.OrderNotCancellable);

            var trip = order.TripBookingOrder.Trip;
            await unitOfWork.TripGroups.AcquireTripLockAsync(trip.TripId, cancellationToken);

            var wasPaid = IsPaid(order);
            var quote = wasPaid ? await refunds.QuoteAsync(RefundOrderInput.From(order), utcNow, cancellationToken) : null;
            if (quote is not null)
                EnsureRefundRequestValid(quote, request, isStaffOrAdmin);

            var pendingCollection = await unitOfWork.Payments.GetLatestPendingCollectionAsync(orderId, cancellationToken);
            if (pendingCollection is not null)
            {
                pendingCollection.Status = PaymentStatuses.Cancelled;
                pendingCollection.ProcessedAt = utcNow;
            }

            var retainedGroupIds = order.TripBookingOrder.Participants
                .Where(p => !p.IsDeleted && p.TripGroupId.HasValue)
                .Select(p => p.TripGroupId!.Value)
                .Distinct()
                .ToList();

            order.Status = OrderStatuses.Cancelled;
            order.CancelledAt = utcNow;
            order.CancelReason = NormalizeOptionalText(request?.Reason) ?? "Hủy booking chuyến đi";
            order.UpdatedAt = utcNow;

            foreach (var participant in order.TripBookingOrder.Participants.Where(p => !p.IsDeleted))
            {
                // Giữ nguyên participant.TripGroupId để truy vết lịch sử, chỉ chuyển Status sang Cancelled
                participant.Status = BookingParticipantStatuses.Cancelled;
                participant.UpdatedAt = utcNow;
            }

            if (wasPaid || retainedGroupIds.Count > 0)
            {
                trip.ProvidedItemsNeedsRecalc = true;
                trip.RowVersion++;
                trip.UpdatedAt = utcNow;
            }

            await unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                ActorUserId = actorUserId,
                Action = "CancelTripBooking",
                EntityType = "TripBookingOrder",
                EntityId = order.OrderId,
                Details = JsonSerializer.Serialize(new
                {
                    tripId = trip.TripId,
                    wasPaid,
                    cancelReason = order.CancelReason,
                    retainedGroupIds,
                    participantIds = order.TripBookingOrder.Participants.Select(p => new { p.ParticipantId, p.TripGroupId })
                }),
                CreatedAt = utcNow.UtcDateTime
            }, cancellationToken);

            // Giữ orders.payment_status = Paid: chỉ đổi khi tiền hoàn thực sự thành công (§4.2)
            var refundCase = quote?.RefundAmount > 0
                ? await refunds.OpenForCancellationAsync(
                    RefundOrderInput.From(order),
                    quote,
                    request!.BankAccount!,
                    request.ConsentVersion!,
                    utcNow,
                    cancellationToken)
                : null;

            var cancelledTourName = trip.Tour?.Name;
            var cancelledContent = string.IsNullOrWhiteSpace(cancelledTourName)
                ? $"Đơn {order.OrderCode} đã được hủy."
                : $"Đơn {order.OrderCode} cho chuyến đi {cancelledTourName.Trim()} đã được hủy.";
            await notificationService.QueueAsync(
                order.CustomerUserId,
                NotificationTypes.OrderCancelled,
                "Đơn hàng đã hủy",
                cancelledContent,
                "Order",
                order.OrderId,
                $"OrderCancelled:{order.OrderId}",
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            var response = MapResponse(order, isStaffOrAdmin ? order.CustomerUserId : actorUserId);
            response.RefundCase = refundCase;
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<InviteLinkResponse> GetInviteLinkAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
        => MapInviteLink(await GetShareableOrderAsync(orderId, cancellationToken));

    public async Task<InviteLinkResponse> RotateInviteLinkAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripBookings.AcquireBookingLockAsync(orderId, cancellationToken);
            var order = await GetShareableOrderAsync(orderId, cancellationToken);
            order.TripBookingOrder.InviteVersion++;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return MapInviteLink(order);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<InvitationPreviewResponse> GetInvitationPreviewAsync(
        Guid orderId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        var order = await GetPreviewableOrderAsync(orderId, token, utcNow, cancellationToken);
        var booking = order.TripBookingOrder;
        var trip = booking.Trip;
        var startDateVn = trip.StartAt.ToOffset(VietnamOffset).Date;
        var isOngoing = trip.StartAt <= utcNow && utcNow < trip.EndAt;

        var activities = (trip.Tour?.Activities ?? [])
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.DayNumber)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.StartTime)
            .Select(a =>
            {
                var dayDate = startDateVn.AddDays(Math.Max(0, a.DayNumber - 1));
                var scheduledStart = new DateTimeOffset(dayDate.Add(a.StartTime), VietnamOffset);
                var scheduledEnd = scheduledStart.AddMinutes(Math.Max(0, a.DurationMinutes));
                var isCurrent = isOngoing && utcNow >= scheduledStart &&
                                (a.DurationMinutes > 0 ? utcNow < scheduledEnd : utcNow == scheduledStart);

                return new TripActivityPreviewResponse
                {
                    ActivityId = a.Id,
                    DayNumber = a.DayNumber,
                    DayTitle = a.DayTitle,
                    StartTime = a.StartTime,
                    DurationMinutes = a.DurationMinutes,
                    DisplayOrder = a.DisplayOrder,
                    Name = a.Name,
                    Description = a.Description,
                    ScheduledStartAt = scheduledStart,
                    ScheduledEndAt = scheduledEnd,
                    IsCurrent = isCurrent
                };
            })
            .ToList();

        return new InvitationPreviewResponse
        {
            OrderId = order.OrderId,
            TourName = trip.Tour?.Name ?? string.Empty,
            TripStartAt = trip.StartAt,
            TripEndAt = trip.EndAt,
            BuyerName = BuyerName(order),
            ExpiresAt = trip.EndAt,
            ParticipantNames = booking.Participants
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.ParticipantId)
                .Select(x => x.FullName)
                .ToList(),
            Activities = activities
        };
    }

    // Booking của người đặt, đã thanh toán đủ và chuyến chưa kết thúc (vẫn cho lấy link khi chuyến đang diễn ra)
    private async Task<Order> GetShareableOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await unitOfWork.TripBookings.GetOwnedBookingAsync(
            orderId,
            currentUser.RequiredUserId,
            cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy booking");

        var paidAmount = await unitOfWork.TripBookings.GetSucceededPaymentTotalAsync(orderId, cancellationToken);
        if (!IsPaid(order) || paidAmount < order.TotalAmount)
            throw new ConflictException("Chỉ lấy link chuyến đi sau khi thanh toán thành công");
        if (order.TripBookingOrder.Trip.EndAt <= timeProvider.GetUtcNow())
            throw new ConflictException("Chuyến đi đã kết thúc");

        return order;
    }

    // Mọi trường hợp link không dùng được trả cùng một lỗi 404 để không lộ booking
    private async Task<Order> GetPreviewableOrderAsync(
        Guid orderId,
        string token,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var order = await unitOfWork.TripBookings.GetBookingAsync(orderId, cancellationToken);
        if (order?.TripBookingOrder is null ||
            !TokenMatches(order, token) ||
            !IsPaid(order) ||
            order.TripBookingOrder.Trip.EndAt <= utcNow)
        {
            throw new NotFoundException("Link chuyến đi không tồn tại hoặc đã hết hiệu lực");
        }

        return order;
    }

    private bool TokenMatches(Order order, string token)
    {
        var expected = inviteTokenHelper.CreateGroupToken(order.OrderId, order.TripBookingOrder.InviteVersion);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token ?? string.Empty));
    }

    private static void EnsureRefundRequestValid(RefundQuoteResult quote, CancelTripBookingRequest? request, bool isStaffOrAdmin)
    {
        // Tài khoản nhận và sự đồng ý phải do chính khách cung cấp
        if (isStaffOrAdmin && quote.RefundAmount > 0)
            throw new ConflictException("Booking đã thanh toán cần khách tự hủy để cung cấp tài khoản nhận hoàn", ErrorCodes.OrderNotCancellable);

        if (!isStaffOrAdmin && (request?.ExpectedRefundAmount ?? 0m) != quote.RefundAmount)
        {
            throw new ConflictException(
                "Số tiền hoàn đã thay đổi, vui lòng xem lại",
                ErrorCodes.RefundAmountChanged,
                new Dictionary<string, string[]> { ["estimatedRefundAmount"] = [quote.RefundAmount.ToString("0", CultureInfo.InvariantCulture)] });
        }

        if (quote.RefundAmount <= 0)
            return;

        var errors = new Dictionary<string, string[]>();
        if (request?.BankAccount is null)
            errors["bankAccount"] = ["Cần cung cấp tài khoản nhận hoàn"];
        if (request?.ConsentAccepted != true || request.ConsentVersion != RefundBankCodes.ConsentVersion)
            errors["consentAccepted"] = ["Bạn cần đồng ý xử lý dữ liệu ngân hàng"];
        if (errors.Count > 0)
            throw new RenTrek.BLL.Exceptions.ValidationException(errors);
    }

    private static bool IsPaid(Order order)
        => string.Equals(order.Status, OrderStatuses.Confirmed, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);

    private static string BuyerName(Order order)
        => !string.IsNullOrWhiteSpace(order.CustomerUser?.FullName)
            ? order.CustomerUser.FullName.Trim()
            : order.TripBookingOrder.Participants.FirstOrDefault(x => x.LinkedUserId == order.CustomerUserId)?.FullName
              ?? "Người đặt";

    // Token nằm sau "#" để không đi vào log server và Referer
    private string BuildInviteUrl(Order order)
    {
        var token = inviteTokenHelper.CreateGroupToken(order.OrderId, order.TripBookingOrder.InviteVersion);
        return $"{emailOptions.Value.FrontendBaseUrl.TrimEnd('/')}/trip-bookings/{order.OrderId}/join#t={token}";
    }

    private InviteLinkResponse MapInviteLink(Order order)
        => new()
        {
            Url = BuildInviteUrl(order),
            ExpiresAt = order.TripBookingOrder.Trip.EndAt,
            TotalSeats = order.TripBookingOrder.Participants.Count
        };

    private static string NormalizeRequiredEmail(string email)
        => NormalizeOptionalEmail(email) ?? throw new BadRequestException("Email người đặt không hợp lệ");

    private static string? NormalizeOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > 255 || !new EmailAddressAttribute().IsValid(normalized))
            throw new BadRequestException("Email participant không hợp lệ");

        return normalized;
    }

    private static string? GetCancelBlockedReason(Order order, DateTimeOffset utcNow)
    {
        if (string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            return "Đơn đã được hủy trước đó";
        if (order.TripBookingOrder.Trip.StartAt <= utcNow)
            return "Chuyến đi đã bắt đầu, không thể hủy booking";
        if (string.Equals(order.PaymentStatus, OrderPaymentStatuses.NeedsReconciliation, StringComparison.OrdinalIgnoreCase))
            return "Đơn đang chờ đối soát thanh toán";

        var unpaidPending = string.Equals(order.Status, OrderStatuses.PendingPayment, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(order.PaymentStatus, OrderPaymentStatuses.Unpaid, StringComparison.OrdinalIgnoreCase);
        return unpaidPending || IsPaid(order)
            ? null
            : "Trạng thái đơn không cho phép hủy";
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ValidateCreateRequest(CreateTripBookingRequest request)
    {
        if (request.TripId == Guid.Empty)
            throw new BadRequestException("TripId là bắt buộc");

        var contactPhone = NormalizeOptionalText(request.ContactPhone);
        if (contactPhone is null || contactPhone.Length > 20)
            throw new BadRequestException("Số điện thoại liên hệ của người đặt là bắt buộc và không vượt quá 20 ký tự");

        var participants = request.AdditionalParticipants ?? [];
        if (participants.Count > 20)
            throw new BadRequestException("Một booking chỉ được thêm tối đa 20 participant");
        if (!request.BuyerParticipates && participants.Count == 0)
            throw new BadRequestException("Đặt giùm phải có ít nhất 1 người đi cùng");

        foreach (var participant in participants)
        {
            if (string.IsNullOrWhiteSpace(participant.FullName) || participant.FullName.Trim().Length > 200)
                throw new BadRequestException("Họ tên participant là bắt buộc và không vượt quá 200 ký tự");
            _ = NormalizeOptionalEmail(participant.Email);
            if (participant.Phone?.Trim().Length > 20 || participant.EmergencyContactPhone?.Trim().Length > 20)
                throw new BadRequestException("Số điện thoại không vượt quá 20 ký tự");
            if (participant.EmergencyContactName?.Trim().Length > 200)
                throw new BadRequestException("Tên liên hệ khẩn cấp không vượt quá 200 ký tự");
        }

        return contactPhone;
    }

    private static List<ParticipantDraft> BuildParticipantDrafts(
        CreateTripBookingRequest request,
        Account buyer,
        string contactPhone)
    {
        var drafts = new List<ParticipantDraft>();
        var buyerName = buyer.FullName.Trim();

        if (request.BuyerParticipates)
        {
            drafts.Add(new ParticipantDraft(
                buyerName,
                NormalizeRequiredEmail(buyer.Email),
                contactPhone,
                null,
                null,
                null));
        }

        foreach (var participant in request.AdditionalParticipants ?? [])
        {
            var phone = NormalizeOptionalText(participant.Phone);
            var emergencyName = phone is null ? buyerName : NormalizeOptionalText(participant.EmergencyContactName);
            var emergencyPhone = phone is null ? contactPhone : NormalizeOptionalText(participant.EmergencyContactPhone);

            drafts.Add(new ParticipantDraft(
                participant.FullName.Trim(),
                NormalizeOptionalEmail(participant.Email),
                phone,
                participant.DateOfBirth,
                emergencyName,
                emergencyPhone));
        }

        return drafts;
    }

    private static void EnsureIdempotentRequestMatches(
        Order existing,
        CreateTripBookingRequest request,
        IReadOnlyList<ParticipantDraft> requestedDrafts)
    {
        var requested = requestedDrafts
            .OrderBy(d => d.Email ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.FullName, StringComparer.Ordinal)
            .ThenBy(d => d.Phone ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var persisted = existing.TripBookingOrder.Participants
            .Select(p => new ParticipantDraft(
                p.FullName.Trim(),
                NormalizeOptionalEmail(p.Email),
                NormalizeOptionalText(p.Phone),
                p.DateOfBirth,
                NormalizeOptionalText(p.EmergencyContactName),
                NormalizeOptionalText(p.EmergencyContactPhone)))
            .OrderBy(d => d.Email ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.FullName, StringComparer.Ordinal)
            .ThenBy(d => d.Phone ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var sameRequest = existing.TripBookingOrder.TripId == request.TripId
            && string.Equals(NormalizeOptionalText(existing.Note), NormalizeOptionalText(request.Note), StringComparison.Ordinal)
            && requested.SequenceEqual(persisted);

        if (!sameRequest)
            throw new ConflictException("Idempotency-Key đã được dùng cho một yêu cầu khác");
    }

    private static TripBookingResponse MapResponse(Order order, Guid userId)
    {
        var canManage = order.CustomerUserId == userId;
        var activeParticipants = string.Equals(order.Status, OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            ? []
            : order.TripBookingOrder.Participants
                .Where(x => !x.IsDeleted && !string.Equals(x.Status, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
                .ToList();
        var participants = canManage
            ? activeParticipants
            : activeParticipants.Where(x => x.LinkedUserId == userId).ToList();

        return new TripBookingResponse
        {
            OrderId = order.OrderId,
            OrderCode = order.OrderCode,
            TripId = order.TripBookingOrder.TripId,
            TripStartAt = order.TripBookingOrder.Trip.StartAt,
            TripEndAt = order.TripBookingOrder.Trip.EndAt,
            TripStatus = order.TripBookingOrder.Trip.Status,
            ParticipantCount = canManage ? order.TripBookingOrder.ParticipantCount : participants.Count,
            UnitPrice = canManage ? order.TripBookingOrder.UnitPrice : 0m,
            SubtotalAmount = canManage ? order.SubtotalAmount : 0m,
            TotalAmount = canManage ? order.TotalAmount : 0m,
            Status = order.Status,
            PaymentStatus = canManage ? order.PaymentStatus : string.Empty,
            HoldExpiresAt = canManage ? order.HoldExpiresAt : null,
            CanManage = canManage,
            Participants = participants.Select(x => new BookingParticipantResponse
            {
                ParticipantId = x.ParticipantId,
                FullName = x.FullName,
                Email = x.Email,
                Phone = x.Phone,
                LinkedUserId = x.LinkedUserId,
                Status = x.Status
            }).ToList()
        };
    }

    private sealed record ParticipantDraft(
        string FullName,
        string? Email,
        string? Phone,
        DateOnly? DateOfBirth,
        string? EmergencyContactName,
        string? EmergencyContactPhone);
}
