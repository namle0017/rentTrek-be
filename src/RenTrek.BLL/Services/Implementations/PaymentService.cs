using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

            if (order.Status != OrderStatuses.PendingPayment || order.PaymentStatus != OrderPaymentStatuses.Unpaid)
                throw new ConflictException("Đơn không ở trạng thái chờ thanh toán", ErrorCodes.OrderNotPayable);

            var now = timeProvider.GetUtcNow();
            if (order.HoldExpiresAt <= now)
                throw new ConflictException("Thời gian giữ chỗ đã hết, vui lòng tạo lại đơn", ErrorCodes.HoldExpired);

            var existing = await unitOfWork.Payments.GetLatestPendingCollectionAsync(orderId, cancellationToken);
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
                Amount = order.TotalAmount,
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

                if (order.Status == OrderStatuses.PendingPayment)
                {
                    order.PaymentStatus = OrderPaymentStatuses.NeedsReconciliation;
                    order.UpdatedAt = now;
                }

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
