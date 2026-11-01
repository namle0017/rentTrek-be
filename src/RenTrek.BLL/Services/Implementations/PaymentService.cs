using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.DTOs.Payment.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
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
    ILogger<PaymentService> logger) : IPaymentService
{
    private const string PaymentTypeCollection = "Collection";
    private const string PaymentMethodBankTransfer = "BankTransfer";
    private const string PurposeTourFee = "TourFee";
    private const string PurposeRentalFee = "RentalFee";

    // Nội dung chuyển khoản = "RTP" + 16 ký tự hex đầu của PaymentId (19 ký tự, dưới giới hạn 25 ký tự của VietQR)
    private const string TransferCodePrefix = "RTP";
    private const int TransferCodeHexLength = 16;


    public async Task<(PaymentResponse Payment, bool Created)> CreateAsync(
        Guid userId,
        Guid orderId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.PaymentMethod?.Trim(), PaymentMethodBankTransfer, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ hỗ trợ phương thức BankTransfer", ErrorCodes.ValidationError);

        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            throw new BadRequestException("Idempotency-Key phải có từ 1 đến 100 ký tự", "IDEMPOTENCY_KEY_REQUIRED");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{orderId:N}:BankTransfer")));

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireIdentityLockAsync($"payment:create:{userId:N}:{key}", cancellationToken);
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.Payments.GetOrderAsync(orderId, userId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn", ErrorCodes.OrderNotFound);

            var replay = await unitOfWork.Repository<PaymentIdempotencyKey>().Query()
                .SingleOrDefaultAsync(x => x.CustomerUserId == userId && x.Key == key, cancellationToken);
            if (replay is not null)
            {
                if (replay.RequestHash != hash) throw new ConflictException("Idempotency-Key đã dùng cho yêu cầu khác", "IDEMPOTENCY_KEY_REUSED");
                var original = await unitOfWork.Payments.GetByIdAsync(replay.PaymentId, cancellationToken)
                    ?? throw new ConflictException("Payment replay không tồn tại", "PAYMENT_REPLAY_UNAVAILABLE");
                await unitOfWork.CommitAsync(cancellationToken);
                return (await MapPaymentAsync(original, order, cancellationToken), false);
            }

            if (order.TotalAmount <= 0 || order.TotalAmount != decimal.Truncate(order.TotalAmount))
                throw new ConflictException("Số tiền đơn không hợp lệ để chuyển khoản.", "ORDER_AMOUNT_INVALID");
            if (order.Status != OrderStatuses.PendingPayment || order.PaymentStatus != OrderPaymentStatuses.Unpaid)
                throw new ConflictException("Đơn không ở trạng thái chờ thanh toán", ErrorCodes.OrderNotPayable);

            var now = timeProvider.GetUtcNow();
            if (order.HoldExpiresAt is null || order.HoldExpiresAt <= now)
                throw new ConflictException("Thời gian giữ chỗ đã hết, vui lòng tạo lại đơn", ErrorCodes.HoldExpired);

            var existing = await unitOfWork.Payments.GetLatestPendingCollectionAsync(orderId, cancellationToken);
            if (existing is not null)
            {
                await unitOfWork.Repository<PaymentIdempotencyKey>().AddAsync(new PaymentIdempotencyKey { CustomerUserId = userId, Key = key, RequestHash = hash, PaymentId = existing.PaymentId, CreatedAt = now }, cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
                return (await MapPaymentAsync(existing, order, cancellationToken), false);
            }

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = order.OrderId,
                PaymentType = PaymentTypeCollection,
                Amount = order.TotalAmount,
                PaymentMethod = PaymentMethodBankTransfer,
                Status = PaymentStatuses.Pending,
                CreatedAt = now,
                Purpose = order.OrderType == OrderTypes.TripBooking ? PurposeTourFee : PurposeRentalFee
            };
            payment.TransferCode = TransferCodePrefix + payment.PaymentId.ToString("N")[..TransferCodeHexLength].ToUpperInvariant();
            await unitOfWork.Payments.AddAsync(payment, cancellationToken);
            await unitOfWork.Repository<PaymentIdempotencyKey>().AddAsync(new PaymentIdempotencyKey { CustomerUserId = userId, Key = key, RequestHash = hash, PaymentId = payment.PaymentId, CreatedAt = now }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            logger.LogInformation("Created payment {PaymentId} for order {OrderId}", payment.PaymentId, order.OrderId);
            return (await MapPaymentAsync(payment, order, cancellationToken), true);
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

        var collected = await unitOfWork.Repository<BankTransaction>().Query().Where(x => x.OrderId == orderId && x.AppliedAt != null).SumAsync(x => x.Amount, cancellationToken);
        var needsReview = payment.Order.PaymentStatus == "NeedsReconciliation" || await unitOfWork.Repository<BankTransaction>().Query()
            .AnyAsync(x => x.OrderId == orderId && x.Status == "NeedsReconciliation", cancellationToken);
        var refundStatus = await unitOfWork.Repository<RefundRequest>().Query().Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.RefundRequestId).Select(x => x.Status).FirstOrDefaultAsync(cancellationToken);

        return new PaymentStatusResponse
        {
            PaymentId = payment.PaymentId,
            Status = payment.Status,
            Amount = payment.Amount,
            ReceivedAmount = payment.ReceivedAmount,
            RemainingAmount = Math.Max(0, payment.Order.TotalAmount - collected),
            ReconciliationStatus = needsReview ? "NeedsReview" : "None",
            RefundStatus = refundStatus,
            ExpiresAt = payment.Order.HoldExpiresAt,
            OrderStatus = payment.Order.Status,
            OrderPaymentStatus = payment.Order.PaymentStatus,
            ProcessedAt = payment.ProcessedAt
        };
    }

    private async Task<PaymentResponse> MapPaymentAsync(Payment payment, Order order, CancellationToken ct)
    {
        var settings = sePayOptions.Value;
        var transferContent = payment.TransferCode ?? TransferCodePrefix + payment.PaymentId.ToString("N")[..TransferCodeHexLength].ToUpperInvariant();
        var funds = await unitOfWork.Repository<BankTransaction>().Query().Where(x => x.OrderId == order.OrderId && x.AppliedAt != null)
            .GroupBy(x => x.PaymentId).Select(x => new { PaymentId = x.Key, Amount = x.Sum(t => t.Amount) }).ToListAsync(ct);
        var remaining = Math.Max(0, order.TotalAmount - funds.Sum(x => x.Amount));
        var amount = remaining.ToString("0", CultureInfo.InvariantCulture);
        var canPay = remaining > 0 && order.Status == "PendingPayment" && order.PaymentStatus == "Unpaid"
            && order.HoldExpiresAt is not null && timeProvider.GetUtcNow() < order.HoldExpiresAt;

        return new PaymentResponse
        {
            PaymentId = payment.PaymentId,
            OrderCode = order.OrderCode,
            Amount = payment.Amount,
            ReceivedAmount = funds.Where(x => x.PaymentId == payment.PaymentId).Sum(x => x.Amount),
            RemainingAmount = remaining,
            Status = payment.Status,
            ExpiresAt = order.HoldExpiresAt,
            BankTransfer = new BankTransferResponse
            {
                BankName = settings.BankName,
                AccountNumber = settings.AccountNumber,
                AccountName = settings.AccountName,
                TransferContent = transferContent,
                QrImageUrl = canPay ? $"https://qr.sepay.vn/img?acc={Uri.EscapeDataString(settings.AccountNumber)}" +
                             $"&bank={Uri.EscapeDataString(settings.BankName)}&amount={amount}&des={transferContent}" : null
            }
        };
    }

}
