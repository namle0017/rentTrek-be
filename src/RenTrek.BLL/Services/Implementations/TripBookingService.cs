using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.DTOs.TripBooking.Requests;
using RenTrek.BLL.DTOs.TripBooking.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ trip booking.
/// </summary>
public sealed class TripBookingService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IInviteTokenHelper inviteTokenHelper,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailOptions,
    ILogger<TripBookingService> logger) : ITripBookingService
{
    private const int MaxInvitationAttempts = 5;

    public async Task<TripBookingResponse> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await unitOfWork.TripBookings.GetForAccessAsync(
            orderId,
            currentUser.RequiredUserId,
            cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy booking");

        return MapResponse(order, currentUser.RequiredUserId);
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
            currentUser.RequiredUserId,
            normalizedKey,
            cancellationToken);
        if (existing is not null)
        {
            EnsureIdempotentRequestMatches(existing, request, currentUser.RequiredUserId);
            return MapResponse(existing, currentUser.RequiredUserId);
        }

        var account = await unitOfWork.TripBookings.GetAccountAsync(currentUser.RequiredUserId, cancellationToken)
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
                currentUser.RequiredUserId,
                normalizedKey,
                cancellationToken);
            existing = await unitOfWork.TripBookings.GetByIdempotencyKeyAsync(
                currentUser.RequiredUserId,
                normalizedKey,
                cancellationToken);
            if (existing is not null)
            {
                EnsureIdempotentRequestMatches(existing, request, currentUser.RequiredUserId);
                await unitOfWork.CommitAsync(cancellationToken);
                return MapResponse(existing, currentUser.RequiredUserId);
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

    public async Task<GroupInvitationResponse> GenerateParticipantInvitationsAsync(
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
                currentUser.RequiredUserId,
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

            var pendingParticipants = order.TripBookingOrder.Participants.Where(x => x.LinkedUserId is null).ToList();
            if (pendingParticipants.Count == 0)
                throw new ConflictException("Tất cả người tham gia đã vào nhóm");

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

            // Một mã chung cho cả nhóm; mỗi participant vẫn có dòng lời mời riêng để biết ai đã vào
            var code = inviteTokenHelper.CreateCode();
            foreach (var participant in pendingParticipants)
            {
                var invitationId = Guid.NewGuid();
                await unitOfWork.TripBookings.AddInvitationAsync(new ParticipantInvitation
                {
                    ParticipantInvitationId = invitationId,
                    ParticipantId = participant.ParticipantId,
                    BookingOrderId = orderId,
                    CreatedByUserId = currentUser.RequiredUserId,
                    TokenHash = HashInvitationCode(invitationId, code),
                    ExpiresAt = expiresAt,
                    CreatedAt = utcNow,
                    Participant = participant,
                    BookingOrder = order.TripBookingOrder
                }, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            // Gửi email sau khi commit: lỗi gửi mail không làm mất lời mời, người đặt vẫn có mã để tự chia sẻ
            var buyerName = order.TripBookingOrder.Participants
                .FirstOrDefault(x => x.LinkedUserId == order.CustomerUserId)?.FullName ?? "Trưởng nhóm";
            var participants = new List<GroupInvitationParticipantResponse>();
            foreach (var participant in pendingParticipants)
            {
                participants.Add(new GroupInvitationParticipantResponse
                {
                    ParticipantId = participant.ParticipantId,
                    FullName = participant.FullName,
                    Email = participant.Email,
                    EmailSent = await TrySendInvitationEmailAsync(order, participant, code, buyerName, expiresAt, cancellationToken)
                });
            }

            return new GroupInvitationResponse { Code = code, ExpiresAt = expiresAt, Participants = participants };
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<InvitationPreviewResponse> GetInvitationPreviewAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await unitOfWork.TripBookings.GetBookingWithInvitationsAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Lời mời không tồn tại hoặc đã hết hiệu lực");
        var outstanding = UsableInvitations(order);
        if (outstanding.Count == 0)
            throw new NotFoundException("Lời mời không tồn tại hoặc đã hết hiệu lực");

        var booking = order.TripBookingOrder;
        return new InvitationPreviewResponse
        {
            OrderId = order.OrderId,
            TourName = booking.Trip.Tour?.Name ?? string.Empty,
            TripStartAt = booking.Trip.StartAt,
            TripEndAt = booking.Trip.EndAt,
            BuyerName = booking.Participants.FirstOrDefault(x => x.LinkedUserId == order.CustomerUserId)?.FullName ?? string.Empty,
            PendingCount = outstanding.Count,
            ExpiresAt = outstanding.Min(x => x.ExpiresAt),
            IsLocked = outstanding.Any(x => x.FailedAttempts >= MaxInvitationAttempts)
        };
    }

    /// <summary>Nhập mã nhóm trước khi đăng nhập: chỉ kiểm tra mã (sai vẫn bị tính lượt), không liên kết tài khoản.</summary>
    public async Task VerifyParticipantInvitationCodeAsync(
        Guid orderId,
        string code,
        CancellationToken cancellationToken = default)
    {
        code = NormalizeInvitationCode(orderId, code);
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var (_, outstanding) = await LockUsableInvitationsAsync(orderId, cancellationToken);
            await EnsureGroupCodeMatchesAsync(outstanding, code, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TripBookingResponse> ClaimParticipantInvitationAsync(
        Guid orderId,
        string code,
        CancellationToken cancellationToken = default)
    {
        code = NormalizeInvitationCode(orderId, code);
        var utcNow = timeProvider.GetUtcNow();
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var (order, outstanding) = await LockUsableInvitationsAsync(orderId, cancellationToken);

            var account = await unitOfWork.TripBookings.GetAccountAsync(currentUser.RequiredUserId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản");
            if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Tài khoản không ở trạng thái hoạt động");
            if (order.CustomerUserId == account.UserId ||
                order.TripBookingOrder.Participants.Any(x => x.LinkedUserId == account.UserId))
            {
                throw new ConflictException("Tài khoản này đã ở trong nhóm");
            }

            // Kiểm tra lại mã khi liên kết: bước verify trước đăng nhập chỉ để trải nghiệm
            await EnsureGroupCodeMatchesAsync(outstanding, code, cancellationToken);

            // Tài khoản được ghép với participant có cùng email trong danh sách người đặt đã khai báo
            var accountEmail = NormalizeEmail(account.Email);
            var invitation = outstanding.FirstOrDefault(x =>
                string.Equals(NormalizeEmail(x.Participant.Email), accountEmail, StringComparison.Ordinal))
                ?? throw new ForbiddenException(
                    "Email tài khoản không có trong danh sách người tham gia. Hãy đăng nhập bằng email trưởng nhóm đã khai báo");

            var participant = invitation.Participant;
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

    private async Task<bool> TrySendInvitationEmailAsync(
        Order order,
        BookingParticipant participant,
        string code,
        string buyerName,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var settings = emailOptions.Value;
        if (!settings.IsConfigured)
        {
            logger.LogWarning("Chưa cấu hình Email SMTP, không gửi được lời mời cho participant {ParticipantId}", participant.ParticipantId);
            return false;
        }

        var trip = order.TripBookingOrder.Trip;
        var link = $"{settings.FrontendBaseUrl.TrimEnd('/')}/trip-bookings/{order.OrderId}/join";
        var vn = TimeSpan.FromHours(7);
        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:520px;margin:auto;color:#1f2937">
              <h2 style="color:#1f4d3a">Bạn được mời tham gia chuyến đi</h2>
              <p>Xin chào <b>{WebUtility.HtmlEncode(participant.FullName)}</b>,</p>
              <p><b>{WebUtility.HtmlEncode(buyerName)}</b> đã thêm bạn vào nhóm chuyến đi
                 <b>{WebUtility.HtmlEncode(trip.Tour?.Name ?? order.OrderCode)}</b>
                 ({trip.StartAt.ToOffset(vn):dd/MM/yyyy HH:mm} – {trip.EndAt.ToOffset(vn):dd/MM/yyyy HH:mm}).</p>
              <p>Mã tham gia nhóm:</p>
              <p style="font-size:32px;letter-spacing:8px;font-weight:bold;color:#1f4d3a">{code}</p>
              <p><a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;background:#1f4d3a;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none">Tham gia nhóm</a></p>
              <p style="font-size:13px;color:#6b7280">Nhập mã, sau đó đăng nhập hoặc tạo tài khoản bằng đúng email {WebUtility.HtmlEncode(participant.Email)}.
                 Mã hết hạn lúc {expiresAt.ToOffset(vn):dd/MM/yyyy HH:mm}.</p>
            </div>
            """;

        try
        {
            await emailSender.SendAsync(
                participant.Email,
                participant.FullName,
                "Lời mời tham gia chuyến đi trên RenTrek",
                body,
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gửi email lời mời thất bại cho participant {ParticipantId}", participant.ParticipantId);
            return false;
        }
    }

    private static string NormalizeInvitationCode(Guid orderId, string? code)
    {
        code = code?.Trim() ?? string.Empty;
        if (orderId == Guid.Empty || code.Length != 6 || !code.All(char.IsAsciiDigit))
            throw new BadRequestException("Mã xác nhận gồm đúng 6 chữ số");
        return code;
    }

    // Lời mời còn dùng được của nhóm: chưa dùng, chưa thu hồi, chưa hết hạn, participant chưa liên kết
    private List<ParticipantInvitation> UsableInvitations(Order order)
    {
        var utcNow = timeProvider.GetUtcNow();
        return order.TripBookingOrder.Invitations
            .Where(x => x.RedeemedAt is null && x.RevokedAt is null && x.ExpiresAt > utcNow &&
                        x.Participant?.LinkedUserId is null)
            .ToList();
    }

    // Khóa booking (advisory lock) rồi tải lời mời của nhóm; gọi trong transaction
    private async Task<(Order Order, List<ParticipantInvitation> Outstanding)> LockUsableInvitationsAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await unitOfWork.TripBookings.AcquireBookingLockAsync(orderId, cancellationToken);
        var order = await unitOfWork.TripBookings.GetBookingWithInvitationsAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Lời mời không tồn tại hoặc đã hết hiệu lực");

        if (!string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Booking chưa đủ điều kiện để nhận lời mời");
        }

        var outstanding = UsableInvitations(order);
        if (outstanding.Count == 0)
            throw new NotFoundException("Lời mời không tồn tại hoặc đã hết hiệu lực");
        if (outstanding.Any(x => x.FailedAttempts >= MaxInvitationAttempts))
            throw new ConflictException("Mã đã bị khóa do nhập sai quá nhiều lần. Hãy nhờ trưởng nhóm gửi lại lời mời");

        return (order, outstanding);
    }

    // Mọi lời mời đang chờ của nhóm dùng chung mã; sai mã thì tăng lượt sai cho cả nhóm và commit trước khi báo lỗi
    private async Task EnsureGroupCodeMatchesAsync(
        IReadOnlyList<ParticipantInvitation> outstanding,
        string code,
        CancellationToken cancellationToken)
    {
        var sample = outstanding[0];
        var expectedHash = Encoding.ASCII.GetBytes(sample.TokenHash);
        var actualHash = Encoding.ASCII.GetBytes(HashInvitationCode(sample.ParticipantInvitationId, code));
        if (CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            return;

        foreach (var invitation in outstanding)
            invitation.FailedAttempts++;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        var remaining = MaxInvitationAttempts - outstanding.Max(x => x.FailedAttempts);
        throw remaining > 0
            ? new BadRequestException($"Mã xác nhận không đúng. Còn {remaining} lần thử")
            : new ConflictException("Mã đã bị khóa do nhập sai quá nhiều lần. Hãy nhờ trưởng nhóm gửi lại lời mời");
    }

    // Băm kèm id lời mời: cùng một mã nhóm vẫn cho hash khác nhau ở từng dòng
    private string HashInvitationCode(Guid invitationId, string code)
        => inviteTokenHelper.HashToken($"{invitationId:N}:{code}");

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
