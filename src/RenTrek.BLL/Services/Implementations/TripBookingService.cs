using RenTrek.BLL.Abstractions;
using RenTrek.BLL.DTOs.TripBooking;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using System.ComponentModel.DataAnnotations;
using ICurrentUserService = RenTrek.BLL.Abstractions.ICurrentUserService;

namespace RenTrek.BLL.Services.Implementations;

public sealed class TripBookingService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IParticipantInviteTokenService inviteTokenService) : ITripBookingService
{
    public async Task<TripBookingResponse> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await unitOfWork.TripBookings.GetForAccessAsync(
            orderId,
            currentUser.UserId,
            cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy booking");

        return MapResponse(order, currentUser.UserId);
    }

    public async Task<TripBookingResponse> CreateAsync(
        CreateTripBookingRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
            throw new BadRequestException("Idempotency-Key là bắt buộc và không vượt quá 100 ký tự");

        var normalizedKey = idempotencyKey.Trim();
        var existing = await unitOfWork.TripBookings.GetByIdempotencyKeyAsync(
            currentUser.UserId,
            normalizedKey,
            cancellationToken);
        if (existing is not null)
        {
            EnsureIdempotentRequestMatches(existing, request, currentUser.UserId);
            return MapResponse(existing, currentUser.UserId);
        }

        var account = await unitOfWork.TripBookings.GetAccountAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản người đặt");
        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản không ở trạng thái hoạt động");

        var buyerEmail = NormalizeEmail(account.Email);
        var additional = request.AdditionalParticipants ?? [];
        var normalizedEmails = additional.Select(x => NormalizeEmail(x.Email)).Prepend(buyerEmail).ToArray();
        if (normalizedEmails.Distinct(StringComparer.Ordinal).Count() != normalizedEmails.Length)
            throw new ConflictException("Email participant không được trùng trong cùng booking");

        var utcNow = timeProvider.GetUtcNow();
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripBookings.AcquireIdempotencyLockAsync(
                currentUser.UserId,
                normalizedKey,
                cancellationToken);
            existing = await unitOfWork.TripBookings.GetByIdempotencyKeyAsync(
                currentUser.UserId,
                normalizedKey,
                cancellationToken);
            if (existing is not null)
            {
                EnsureIdempotentRequestMatches(existing, request, currentUser.UserId);
                await unitOfWork.CommitAsync(cancellationToken);
                return MapResponse(existing, currentUser.UserId);
            }

            var trip = await unitOfWork.TripBookings.GetTripForUpdateAsync(request.TripId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chuyến đi");
            if (!string.Equals(trip.Status, "Open", StringComparison.OrdinalIgnoreCase) || trip.StartAt <= utcNow)
                throw new ConflictException("Chuyến đi không mở nhận booking");

            var participantCount = additional.Count + 1;
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
                Participants =
                [
                    new BookingParticipant
                    {
                        ParticipantId = Guid.NewGuid(),
                        BookingOrderId = orderId,
                        LinkedUserId = account.UserId,
                        FullName = account.FullName.Trim(),
                        Email = buyerEmail,
                        Phone = account.Phone,
                        Status = "Pending",
                        TripId = trip.TripId,
                        CreatedAt = utcNow,
                        UpdatedAt = utcNow
                    }
                ]
            };

            foreach (var participant in additional)
            {
                booking.Participants.Add(new BookingParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    BookingOrderId = orderId,
                    FullName = participant.FullName.Trim(),
                    Email = NormalizeEmail(participant.Email),
                    Phone = participant.Phone?.Trim(),
                    DateOfBirth = participant.DateOfBirth,
                    EmergencyContactName = participant.EmergencyContactName?.Trim(),
                    EmergencyContactPhone = participant.EmergencyContactPhone?.Trim(),
                    Status = "Pending",
                    TripId = trip.TripId,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }

            var order = new Order
            {
                OrderId = orderId,
                CustomerUserId = account.UserId,
                OrderType = "TripBooking",
                TotalAmount = subtotal,
                SubtotalAmount = subtotal,
                DiscountAmount = 0m,
                Status = "PendingPayment",
                PaymentStatus = "Unpaid",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                OrderCode = $"RT-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..32].ToUpperInvariant(),
                Note = request.Note?.Trim(),
                HoldExpiresAt = utcNow.AddMinutes(15),
                RefundPolicyId = trip.RefundPolicyId,
                IdempotencyKey = normalizedKey,
                TripBookingOrder = booking
            };
            booking.Order = order;

            await unitOfWork.TripBookings.AddOrderAsync(order, cancellationToken);
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

    public async Task<IReadOnlyList<ParticipantInvitationResponse>> GenerateParticipantInvitationsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.TripBookings.AcquireBookingLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.TripBookings.GetOwnedBookingAsync(
                orderId,
                currentUser.UserId,
                cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy booking");

            var paidAmount = await unitOfWork.TripBookings.GetSucceededPaymentTotalAsync(
                orderId,
                cancellationToken);
            if (!string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                paidAmount < order.TotalAmount)
            {
                throw new ConflictException("Chỉ tạo lời mời sau khi thanh toán thành công");
            }

            var activeInvitations = await unitOfWork.TripBookings.GetOutstandingInvitationsAsync(
                orderId,
                cancellationToken);
            foreach (var activeInvitation in activeInvitations)
                activeInvitation.RevokedAt = utcNow;

            if (activeInvitations.Count > 0)
                await unitOfWork.SaveChangesAsync(cancellationToken);

            var expiresAt = utcNow.AddDays(7);
            if (order.TripBookingOrder.Trip.StartAt < expiresAt)
                expiresAt = order.TripBookingOrder.Trip.StartAt;
            if (expiresAt <= utcNow)
                throw new ConflictException("Chuyến đi đã bắt đầu hoặc lời mời không còn hiệu lực");

            var responses = new List<ParticipantInvitationResponse>();
            foreach (var participant in order.TripBookingOrder.Participants.Where(x => x.LinkedUserId is null))
            {
                var token = inviteTokenService.CreateToken();
                var invitation = new ParticipantInvitation
                {
                    ParticipantInvitationId = Guid.NewGuid(),
                    ParticipantId = participant.ParticipantId,
                    BookingOrderId = orderId,
                    CreatedByUserId = currentUser.UserId,
                    TokenHash = inviteTokenService.HashToken(token),
                    ExpiresAt = expiresAt,
                    CreatedAt = utcNow,
                    Participant = participant,
                    BookingOrder = order.TripBookingOrder
                };
                await unitOfWork.TripBookings.AddInvitationAsync(invitation, cancellationToken);
                responses.Add(new ParticipantInvitationResponse
                {
                    ParticipantId = participant.ParticipantId,
                    FullName = participant.FullName,
                    Email = participant.Email,
                    Token = token,
                    ExpiresAt = expiresAt
                });
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return responses;
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TripBookingResponse> ClaimParticipantInvitationAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new BadRequestException("Token lời mời là bắt buộc");

        var utcNow = timeProvider.GetUtcNow();
        var tokenHash = inviteTokenService.HashToken(token.Trim());
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var bookingOrderId = await unitOfWork.TripBookings.GetInvitationBookingOrderIdAsync(
                tokenHash,
                cancellationToken)
                ?? throw new NotFoundException("Lời mời không tồn tại");
            await unitOfWork.TripBookings.AcquireBookingLockAsync(bookingOrderId, cancellationToken);

            var invitation = await unitOfWork.TripBookings.GetInvitationByTokenHashAsync(
                tokenHash,
                cancellationToken)
                ?? throw new NotFoundException("Lời mời không tồn tại");

            if (invitation.RevokedAt is not null || invitation.RedeemedAt is not null || invitation.ExpiresAt <= utcNow)
                throw new ConflictException("Lời mời đã hết hiệu lực hoặc đã được sử dụng");

            var order = invitation.BookingOrder.Order;
            if (!string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("Booking chưa đủ điều kiện để nhận lời mời");
            }

            var participant = invitation.Participant;
            if (participant.LinkedUserId is not null)
                throw new ConflictException("Participant đã được liên kết tài khoản");

            var account = await unitOfWork.TripBookings.GetAccountAsync(currentUser.UserId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản");
            if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Tài khoản không ở trạng thái hoạt động");
            if (!string.Equals(NormalizeEmail(account.Email), NormalizeEmail(participant.Email), StringComparison.Ordinal))
                throw new ForbiddenException("Email tài khoản không khớp participant được mời");

            participant.LinkedUserId = account.UserId;
            participant.UpdatedAt = utcNow;
            invitation.RedeemedAt = utcNow;
            invitation.RedeemedByUserId = account.UserId;

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

    private static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length == 0 || normalized.Length > 255 || !new EmailAddressAttribute().IsValid(normalized))
            throw new BadRequestException("Email participant không hợp lệ");
        return normalized;
    }

    private static void ValidateCreateRequest(CreateTripBookingRequest request)
    {
        if (request.TripId == Guid.Empty)
            throw new BadRequestException("TripId là bắt buộc");

        var participants = request.AdditionalParticipants ?? [];
        if (participants.Count > 20)
            throw new BadRequestException("Một booking chỉ được thêm tối đa 20 participant");

        foreach (var participant in participants)
        {
            if (string.IsNullOrWhiteSpace(participant.FullName) || participant.FullName.Trim().Length > 200)
                throw new BadRequestException("Họ tên participant là bắt buộc và không vượt quá 200 ký tự");
            _ = NormalizeEmail(participant.Email);
            if (participant.Phone?.Trim().Length > 20 || participant.EmergencyContactPhone?.Trim().Length > 20)
                throw new BadRequestException("Số điện thoại không vượt quá 20 ký tự");
            if (participant.EmergencyContactName?.Trim().Length > 200)
                throw new BadRequestException("Tên liên hệ khẩn cấp không vượt quá 200 ký tự");
        }
    }

    private static void EnsureIdempotentRequestMatches(
        Order existing,
        CreateTripBookingRequest request,
        Guid buyerUserId)
    {
        var requestedParticipants = (request.AdditionalParticipants ?? [])
            .Select(participant => (
                Email: NormalizeEmail(participant.Email),
                FullName: participant.FullName.Trim(),
                Phone: participant.Phone?.Trim(),
                participant.DateOfBirth,
                EmergencyContactName: participant.EmergencyContactName?.Trim(),
                EmergencyContactPhone: participant.EmergencyContactPhone?.Trim()))
            .OrderBy(participant => participant.Email, StringComparer.Ordinal)
            .ToArray();

        var existingParticipants = existing.TripBookingOrder.Participants
            .Where(participant => participant.LinkedUserId != buyerUserId)
            .Select(participant => (
                Email: NormalizeEmail(participant.Email),
                FullName: participant.FullName.Trim(),
                Phone: participant.Phone?.Trim(),
                participant.DateOfBirth,
                EmergencyContactName: participant.EmergencyContactName?.Trim(),
                EmergencyContactPhone: participant.EmergencyContactPhone?.Trim()))
            .OrderBy(participant => participant.Email, StringComparer.Ordinal)
            .ToArray();

        var sameRequest = existing.TripBookingOrder.TripId == request.TripId
            && string.Equals(existing.Note?.Trim(), request.Note?.Trim(), StringComparison.Ordinal)
            && requestedParticipants.SequenceEqual(existingParticipants);

        if (!sameRequest)
            throw new ConflictException("Idempotency-Key đã được dùng cho một yêu cầu khác");
    }

    private static TripBookingResponse MapResponse(Order order, Guid userId)
    {
        var canManage = order.CustomerUserId == userId;
        var participants = canManage
            ? order.TripBookingOrder.Participants
            : order.TripBookingOrder.Participants.Where(x => x.LinkedUserId == userId).ToList();

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
                LinkedUserId = x.LinkedUserId,
                Status = x.Status
            }).ToList()
        };
    }
}
