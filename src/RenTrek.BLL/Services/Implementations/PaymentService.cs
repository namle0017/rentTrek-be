using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.DTOs.Payment.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ thanh toán chuyển khoản VietQR qua SePay: tạo hướng dẫn chuyển khoản, theo dõi trạng thái và xử lý webhook.
/// </summary>
public sealed partial class PaymentService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<SePaySettings> sePayOptions,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailOptions,
    ITripGroupingService tripGroupingService,
    INotificationService notificationService,
    ILogger<PaymentService> logger,
    IRentalPostPaymentService? rentalPostPaymentService = null,
    ITripReschedulePaymentHandler? tripRescheduleService = null) : IPaymentService
{
    private const string PaymentTypeCollection = "Collection";
    private const string PaymentMethodBankTransfer = "BankTransfer";
    private const string PurposeTourFee = "TourFee";
    private const string PurposeRentalFee = "RentalFee";

    // Nội dung chuyển khoản = "RTP" + 16 ký tự hex đầu của PaymentId (19 ký tự, dưới giới hạn 25 ký tự của VietQR)
    private const string TransferCodePrefix = "RTP";
    private const int TransferCodeHexLength = 16;

    [GeneratedRegex("RTP([0-9A-F]{16})")]
    private static partial Regex TransferCodeRegex();

    public async Task<(PaymentResponse Payment, bool Created)> CreateAsync(
        Guid userId,
        Guid orderId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.PaymentMethod?.Trim(), PaymentMethodBankTransfer, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ hỗ trợ phương thức BankTransfer", ErrorCodes.ValidationError);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.Payments.GetOrderAsync(orderId, userId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn", ErrorCodes.OrderNotFound);

            var existing = await unitOfWork.Payments.GetLatestPendingCollectionAsync(orderId, cancellationToken);

            // Khoản thu sau (gia hạn / phí vượt cọc) đã được tạo sẵn: trả lại hướng dẫn chuyển khoản của nó
            if (existing is not null && PaymentPurposes.AfterOrderPaid.Contains(existing.Purpose))
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return (MapPayment(existing, order), false);
            }

            if (order.Status != OrderStatuses.PendingPayment || order.PaymentStatus != OrderPaymentStatuses.Unpaid)
                throw new ConflictException("Đơn không ở trạng thái chờ thanh toán", ErrorCodes.OrderNotPayable);

            var now = timeProvider.GetUtcNow();
            if (order.HoldExpiresAt <= now)
                throw new ConflictException("Thời gian giữ chỗ đã hết, vui lòng tạo lại đơn", ErrorCodes.HoldExpired);

            if (existing is not null)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return (MapPayment(existing, order), false);
            }

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = order.OrderId,
                PaymentType = PaymentTypeCollection,
                // Đơn thuê: tiền thuê + cọc trong một lần chuyển khoản
                Amount = OrderPayableAmount.Of(order),
                PaymentMethod = PaymentMethodBankTransfer,
                Status = PaymentStatuses.Pending,
                CreatedAt = now,
                Purpose = order.OrderType == OrderTypes.TripBooking ? PurposeTourFee : PurposeRentalFee
            };
            await unitOfWork.Payments.AddAsync(payment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return (MapPayment(payment, order), true);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PaymentStatusResponse> GetStatusAsync(
        Guid userId,
        Guid orderId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await unitOfWork.Payments.GetOwnedAsync(paymentId, orderId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy lần thanh toán", ErrorCodes.PaymentNotFound);

        return new PaymentStatusResponse
        {
            PaymentId = payment.PaymentId,
            Status = payment.Status,
            Amount = payment.Amount,
            ExpiresAt = payment.Order.HoldExpiresAt,
            OrderStatus = payment.Order.Status,
            OrderPaymentStatus = payment.Order.PaymentStatus,
            ProcessedAt = payment.ProcessedAt
        };
    }

    public async Task HandleSePayWebhookAsync(
        string? authorization,
        SePayWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureSePayAuthorized(authorization);

        if (!string.Equals(request.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            return;

        var match = TransferCodeRegex().Match($"{request.Code} {request.Content}".ToUpperInvariant());
        if (!match.Success)
        {
            logger.LogWarning("SePay giao dịch {TransactionId} không chứa mã thanh toán: {Content}", request.Id, request.Content);
            return;
        }

        var candidate = await unitOfWork.Payments.FindByIdPrefixAsync(
            match.Groups[1].Value.ToLowerInvariant(),
            cancellationToken);
        if (candidate is null)
        {
            logger.LogWarning("SePay giao dịch {TransactionId} có mã {Code} không khớp thanh toán nào", request.Id, match.Value);
            return;
        }

        var providerTransactionId = $"SEPAY:{request.Id}";
        Order? confirmedTripOrder = null;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(candidate.OrderId, cancellationToken);
            if (await unitOfWork.Payments.ProviderTransactionExistsAsync(providerTransactionId, cancellationToken))
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return;
            }

            var payment = await unitOfWork.Payments.GetByIdAsync(candidate.PaymentId, cancellationToken);
            var order = await unitOfWork.Payments.GetOrderAsync(candidate.OrderId, cancellationToken: cancellationToken);
            if (payment is null || order is null)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return;
            }

            var now = timeProvider.GetUtcNow();
            var isAfterPaidCollection = PaymentPurposes.AfterOrderPaid.Contains(payment.Purpose);

            if (isAfterPaidCollection)
            {
                // Gia hạn / phí vượt cọc: đơn đã thanh toán từ trước, chỉ cần đúng khoản đang chờ và đúng số tiền
                var afterPaidPayable = payment.Status == PaymentStatuses.Pending
                                       && request.TransferAmount == payment.Amount
                                       && order.Status is not (OrderStatuses.Cancelled or OrderStatuses.Expired);
                if (afterPaidPayable && payment.Purpose == PaymentPurposes.TripReschedule && tripRescheduleService is not null)
                {
                    payment.Status = PaymentStatuses.Succeeded;
                    payment.ProviderTransactionId = providerTransactionId;
                    payment.ProcessedAt = now;
                    await tripRescheduleService.ApplySucceededPaymentAsync(payment, now, cancellationToken);
                }
                else if (afterPaidPayable && payment.Purpose != PaymentPurposes.TripReschedule && rentalPostPaymentService is not null)
                {
                    payment.Status = PaymentStatuses.Succeeded;
                    payment.ProviderTransactionId = providerTransactionId;
                    payment.ProcessedAt = now;
                    await rentalPostPaymentService.ApplySucceededPaymentAsync(payment, order, now, cancellationToken);
                }
                else
                {
                    // Không đổi trạng thái thanh toán của đơn (đơn vẫn Paid); ghi dòng chờ đối soát cho admin
                    await unitOfWork.Payments.AddAsync(new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        OrderId = order.OrderId,
                        PaymentType = PaymentTypeCollection,
                        Amount = request.TransferAmount,
                        PaymentMethod = PaymentMethodBankTransfer,
                        ProviderTransactionId = providerTransactionId,
                        Status = PaymentStatuses.PendingApproval,
                        CreatedAt = now,
                        Purpose = payment.Purpose
                    }, cancellationToken);
                    logger.LogWarning(
                        "SePay giao dịch {TransactionId} ({Amount}) cho khoản thu sau của đơn {OrderCode} cần đối soát",
                        request.Id,
                        request.TransferAmount,
                        order.OrderCode);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
                return;
            }

            var payable = payment.Status == PaymentStatuses.Pending
                          && order.Status == OrderStatuses.PendingPayment
                          && order.PaymentStatus == OrderPaymentStatuses.Unpaid
                          && !(order.HoldExpiresAt <= now)
                          && request.TransferAmount == payment.Amount;

            if (payable)
            {
                payment.Status = PaymentStatuses.Succeeded;
                payment.ProviderTransactionId = providerTransactionId;
                payment.ProcessedAt = now;
                order.Status = OrderStatuses.Confirmed;
                order.PaymentStatus = OrderPaymentStatuses.Paid;
                order.UpdatedAt = now;

                if (order.OrderType == OrderTypes.TripBooking)
                {
                    confirmedTripOrder = await LinkTripParticipantsAsync(order.OrderId, now, cancellationToken);
                    await tripGroupingService.AutoAssignPaidBookingInTransactionAsync(order.OrderId, cancellationToken);
                }

                var confirmedTourName = confirmedTripOrder?.TripBookingOrder?.Trip?.Tour?.Name
                    ?? order.TripBookingOrder?.Trip?.Tour?.Name;
                var confirmedContent = !string.IsNullOrWhiteSpace(confirmedTourName)
                    ? $"Đơn {order.OrderCode} cho chuyến đi {confirmedTourName.Trim()} đã được xác nhận."
                    : order.OrderType == OrderTypes.Rental
                        ? $"Đơn thuê thiết bị {order.OrderCode} đã được xác nhận."
                        : $"Đơn {order.OrderCode} đã được xác nhận.";
                await notificationService.QueueAsync(
                    order.CustomerUserId,
                    NotificationTypes.OrderConfirmed,
                    "Đơn hàng đã được xác nhận",
                    confirmedContent,
                    "Order",
                    order.OrderId,
                    $"OrderConfirmed:{order.OrderId}",
                    cancellationToken);
            }
            else
            {
                // Tiền đã vào nhưng không tự xác nhận được (sai số tiền / hết hạn giữ chỗ / đơn đã xử lý): ghi lại để admin đối soát
                await unitOfWork.Payments.AddAsync(new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    OrderId = order.OrderId,
                    PaymentType = PaymentTypeCollection,
                    Amount = request.TransferAmount,
                    PaymentMethod = PaymentMethodBankTransfer,
                    ProviderTransactionId = providerTransactionId,
                    Status = PaymentStatuses.Pending,
                    CreatedAt = now,
                    Purpose = payment.Purpose
                }, cancellationToken);

                order.PaymentStatus = OrderPaymentStatuses.NeedsReconciliation;
                order.UpdatedAt = now;

                logger.LogWarning(
                    "SePay giao dịch {TransactionId} ({Amount}) cho đơn {OrderCode} cần đối soát",
                    request.Id,
                    request.TransferAmount,
                    order.OrderCode);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (confirmedTripOrder?.TripBookingOrder is not null)
        {
            await NotifyTripParticipantsAsync(confirmedTripOrder, cancellationToken);
        }
    }

    private async Task<Order?> LinkTripParticipantsAsync(
        Guid orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var bookingOrder = await unitOfWork.TripBookings.GetBookingAsync(orderId, cancellationToken);
        if (bookingOrder?.TripBookingOrder is not { } booking || booking.Trip.StartAt <= now)
            return bookingOrder;

        // Booking tạo trước khi gắn sẵn người đặt: email trùng người đặt thì gắn luôn, không cần xác thực
        var buyerEmail = bookingOrder.CustomerUser?.Email?.Trim();
        foreach (var participant in booking.Participants.Where(p => p.LinkedUserId is null &&
                     !string.IsNullOrEmpty(buyerEmail) &&
                     string.Equals(p.Email?.Trim(), buyerEmail, StringComparison.OrdinalIgnoreCase)))
        {
            participant.LinkedUserId = bookingOrder.CustomerUserId;
            participant.UpdatedAt = now;
        }

        var emails = booking.Participants
            .Where(p => p.LinkedUserId is null && !string.IsNullOrWhiteSpace(p.Email))
            .Select(p => p.Email!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (emails.Length == 0)
            return bookingOrder;

        var confirmedAccounts = await unitOfWork.TripBookings.GetConfirmedAccountsByEmailsAsync(emails, cancellationToken);
        var userIdsByEmail = confirmedAccounts
            .Where(a => a.EmailConfirmedAt is not null &&
                        string.Equals(a.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
                        !a.IsDeleted)
            .ToDictionary(a => a.Email.Trim().ToLowerInvariant(), a => a.UserId, StringComparer.Ordinal);

        foreach (var participant in booking.Participants.Where(p => p.LinkedUserId is null && !string.IsNullOrWhiteSpace(p.Email)))
        {
            if (userIdsByEmail.TryGetValue(participant.Email!.Trim().ToLowerInvariant(), out var userId))
            {
                participant.LinkedUserId = userId;
                participant.UpdatedAt = now;
            }
        }

        return bookingOrder;
    }

    private async Task NotifyTripParticipantsAsync(Order order, CancellationToken cancellationToken)
    {
        var settings = emailOptions.Value;
        var booking = order.TripBookingOrder;
        var activeParticipants = booking.Participants
            .Where(p => !p.IsDeleted && !string.Equals(p.Status, BookingParticipantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.CreatedAt)
            .ToList();

        // Người đặt + mọi người tham gia có email đều nhận mail đặt thành công kèm bill
        var recipients = new List<(string Email, string Name)>();
        if (!string.IsNullOrWhiteSpace(order.CustomerUser?.Email))
            recipients.Add((order.CustomerUser.Email.Trim(), order.CustomerUser.FullName?.Trim() ?? "bạn"));
        foreach (var p in activeParticipants.Where(p => !string.IsNullOrWhiteSpace(p.Email)))
        {
            if (!recipients.Any(r => string.Equals(r.Email, p.Email!.Trim(), StringComparison.OrdinalIgnoreCase)))
                recipients.Add((p.Email!.Trim(), p.FullName));
        }

        if (recipients.Count == 0)
            return;

        if (!settings.IsConfigured)
        {
            logger.LogWarning("Chưa cấu hình Email SMTP, bỏ qua thông báo chuyến đi cho đơn {OrderCode}", order.OrderCode);
            return;
        }

        var trip = booking.Trip;
        var tourName = trip.Tour?.Name ?? order.OrderCode;
        var vn = TimeSpan.FromHours(7);
        var money = (decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
        var td = "padding:6px 0;border-bottom:1px solid #e5e7eb";
        var names = string.Join("", activeParticipants.Select((p, i) =>
            $"<li>{i + 1}. {WebUtility.HtmlEncode(p.FullName)}</li>"));
        var discountRow = order.DiscountAmount > 0
            ? $"""<tr><td style="{td}">Giảm giá</td><td style="{td};text-align:right">-{money(order.DiscountAmount)}</td></tr>"""
            : string.Empty;

        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:520px;margin:auto;color:#1f2937">
              <h2 style="color:#1f4d3a">Đặt chuyến thành công</h2>
              <p>Thanh toán cho đơn <b>{WebUtility.HtmlEncode(order.OrderCode)}</b> đã được xác nhận. Tóm tắt chuyến đi:</p>
              <table style="width:100%;border-collapse:collapse;font-size:14px">
                <tr><td style="{td}">Tour</td><td style="{td};text-align:right"><b>{WebUtility.HtmlEncode(tourName)}</b></td></tr>
                <tr><td style="{td}">Khởi hành</td><td style="{td};text-align:right">{trip.StartAt.ToOffset(vn):dd/MM/yyyy HH:mm}</td></tr>
                <tr><td style="{td}">Kết thúc</td><td style="{td};text-align:right">{trip.EndAt.ToOffset(vn):dd/MM/yyyy HH:mm}</td></tr>
                <tr><td style="{td}">Số người</td><td style="{td};text-align:right">{booking.ParticipantCount} × {money(booking.UnitPrice)}</td></tr>
                <tr><td style="{td}">Tạm tính</td><td style="{td};text-align:right">{money(order.SubtotalAmount)}</td></tr>
                {discountRow}
                <tr><td style="padding:8px 0"><b>Đã thanh toán</b></td><td style="padding:8px 0;text-align:right;color:#1f4d3a"><b>{money(order.TotalAmount)}</b></td></tr>
              </table>
              <p style="margin-top:16px"><b>Người tham gia</b></p>
              <ul style="list-style:none;padding:0;margin:0">{names}</ul>
            </div>
            """;

        foreach (var (email, name) in recipients)
        {
            try
            {
                await emailSender.SendAsync(
                    email,
                    name,
                    $"Đặt chuyến thành công - {tourName}",
                    body,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Gửi email đặt chuyến thành công thất bại cho đơn {OrderCode}", order.OrderCode);
            }
        }
    }

    private void EnsureSePayAuthorized(string? authorization)
    {
        var apiKey = sePayOptions.Value.Api.Key;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(authorization))
            throw new UnauthorizedException("API key webhook không hợp lệ");

        var expected = Encoding.UTF8.GetBytes($"Apikey {apiKey}");
        var actual = Encoding.UTF8.GetBytes(authorization.Trim());
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new UnauthorizedException("API key webhook không hợp lệ");
    }

    private PaymentResponse MapPayment(Payment payment, Order order)
    {
        var settings = sePayOptions.Value;
        var transferContent = TransferCodePrefix + payment.PaymentId.ToString("N")[..TransferCodeHexLength].ToUpperInvariant();
        var amount = payment.Amount.ToString("0", CultureInfo.InvariantCulture);

        return new PaymentResponse
        {
            PaymentId = payment.PaymentId,
            OrderCode = order.OrderCode,
            Amount = payment.Amount,
            Status = payment.Status,
            ExpiresAt = order.HoldExpiresAt,
            BankTransfer = new BankTransferResponse
            {
                BankName = settings.BankName,
                AccountNumber = settings.AccountNumber,
                AccountName = settings.AccountName,
                TransferContent = transferContent,
                QrImageUrl = $"https://qr.sepay.vn/img?acc={Uri.EscapeDataString(settings.AccountNumber)}" +
                             $"&bank={Uri.EscapeDataString(settings.BankName)}&amount={amount}&des={transferContent}"
            }
        };
    }
}
