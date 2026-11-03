using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Điều phối hồ sơ hoàn tiền thủ công theo máy trạng thái ở docs/05-MANUAL-REFUND-WORKFLOW.md §4.
/// </summary>
public sealed class RefundCaseService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IRefundDataProtector dataProtector,
    IRefundVerificationService verificationService,
    IEmailSender emailSender,
    ILogger<RefundCaseService> logger) : IRefundCaseService
{
    private const string PaymentTypeRefund = "Refund";
    private const string PaymentMethodBankTransfer = "BankTransfer";
    private const string PurposeTourFee = "TourFee";
    private const string PurposeRentalFee = "RentalFee";
    private const string EntityType = "RefundCase";
    private const int MaxPageSize = 100;

    private static readonly TimeSpan PayoutRetention = TimeSpan.FromDays(30);
    private static readonly string[] AdminOnly = [RoleNames.Admin];

    public async Task<RefundQuoteResult> QuoteAsync(Order order, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var policy = await unitOfWork.RefundCases.GetPolicyWithRulesAsync(order.RefundPolicyId, cancellationToken);
        var paidAmount = order.PaymentStatus == OrderPaymentStatuses.Paid
            ? await unitOfWork.TripBookings.GetSucceededPaymentTotalAsync(order.OrderId, cancellationToken)
            : 0m;

        var hoursBefore = (order.TripBookingOrder.Trip.StartAt - utcNow).TotalHours;
        var rule = policy?.RefundPolicyRules
            .Where(x => x.MinHoursBefore <= hoursBefore)
            .MaxBy(x => x.MinHoursBefore);

        return new RefundQuoteResult
        {
            PaidAmount = paidAmount,
            RefundAmount = rule is null ? 0m : Math.Floor(paidAmount * rule.RefundPercentage / 100m),
            AppliedCase = rule is null || rule.MinHoursBefore == 0
                ? "Hủy sát giờ khởi hành"
                : $"Hủy trước giờ khởi hành từ {rule.MinHoursBefore} giờ trở lên",
            Policy = policy,
            Rule = rule
        };
    }

    public async Task<RefundCaseSummaryResponse> OpenForCancellationAsync(
        Order order,
        RefundQuoteResult quote,
        RefundBankAccountRequest bankAccount,
        string consentVersion,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var originalPaymentId = await unitOfWork.RefundCases.GetLatestCollectionPaymentIdAsync(order.OrderId, cancellationToken);
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            OriginalPaymentId = originalPaymentId,
            PaymentType = PaymentTypeRefund,
            Amount = quote.RefundAmount,
            PaymentMethod = PaymentMethodBankTransfer,
            Status = PaymentStatuses.PendingApproval,
            CreatedAt = utcNow,
            RefundPolicyRuleId = quote.Rule?.RefundPolicyRuleId,
            Purpose = order.OrderType == OrderTypes.TripBooking ? PurposeTourFee : PurposeRentalFee
        };
        await unitOfWork.Payments.AddAsync(payment, cancellationToken);

        var accountNumber = bankAccount.AccountNumber.Trim();
        var refund = new RefundCase
        {
            RefundCaseId = Guid.NewGuid(),
            OrderId = order.OrderId,
            RefundPaymentId = payment.PaymentId,
            CustomerUserId = order.CustomerUserId,
            RefundPolicyRuleId = quote.Rule?.RefundPolicyRuleId,
            Amount = quote.RefundAmount,
            Status = RefundCaseStatuses.Requested,
            BankCode = bankAccount.BankCode,
            BankAccountLast4 = accountNumber[^4..],
            ConsentVersion = consentVersion,
            ConsentAcceptedAt = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var holder = Regex.Replace(bankAccount.AccountHolder.Trim(), @"\s+", " ").ToUpperInvariant();
        var protectedPayload = dataProtector.Protect(new RefundPayoutDetails(accountNumber, holder), refund.RefundCaseId, order.OrderId);
        refund.PayoutDetailsCiphertext = protectedPayload.Ciphertext;
        refund.PayoutDetailsNonce = protectedPayload.Nonce;
        refund.PayoutDetailsTag = protectedPayload.Tag;

        await unitOfWork.RefundCases.AddAsync(refund, cancellationToken);
        await AuditAsync(order.CustomerUserId, "RefundCaseCreated", refund, utcNow, cancellationToken);
        await NotifyRolesAsync(AdminOnly, "Hồ sơ hoàn tiền mới", $"Đơn {order.OrderCode} chờ xuất cho kế toán.", refund, utcNow, cancellationToken);
        return MapSummary(refund);
    }

    public async Task<RefundCaseSummaryResponse?> GetSummaryByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var refund = await unitOfWork.RefundCases.GetLatestByOrderAsync(orderId, cancellationToken);
        return refund is null ? null : MapSummary(refund);
    }

    public async Task<RefundCaseResponse> GetAsync(Guid refundCaseId, CancellationToken cancellationToken = default)
    {
        var refund = await unitOfWork.RefundCases.GetDetailAsync(refundCaseId, cancellationToken);
        EnsureOwned(refund);
        return MapCustomer(refund!);
    }

    public Task<PagedResponse<RefundCaseQueueItemResponse>> GetAdminQueueAsync(
        RefundCaseQueryRequest request,
        CancellationToken cancellationToken = default)
        => GetQueueAsync(RefundCaseStatuses.AdminQueue, request, cancellationToken);

    public async Task<RefundChallengeResponse> RequestExportCodeAsync(CancellationToken cancellationToken = default)
    {
        var adminId = currentUser.RequiredUserId;
        var admin = await unitOfWork.Accounts.GetByIdWithRoleAsync(adminId, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản");
        if (admin.EmailConfirmedAt is null)
            throw new ForbiddenException("Email của Admin chưa được xác minh");

        var (challenge, code) = await verificationService.IssueAsync(adminId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Gửi sau khi lưu để lỗi SMTP không làm mất giới hạn số lần gửi
        await emailSender.SendAsync(
            admin.Email,
            admin.FullName,
            "Mã xác thực hoàn tiền RenTrekk",
            $"<p>Mã xác thực của bạn là <strong>{code}</strong>. Mã có hiệu lực trong 5 phút.</p>",
            cancellationToken);
        return new RefundChallengeResponse { ChallengeId = challenge.ChallengeId, ExpiresAt = challenge.ExpiresAt };
    }

    public async Task<RefundExportFileResponse> ExportAsync(RefundExportRequest request, CancellationToken cancellationToken = default)
    {
        var adminId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await VerifyOtpAsync(adminId, request, cancellationToken);

            var status = request.ReExport ? RefundCaseStatuses.Approved : RefundCaseStatuses.Requested;
            var refunds = await unitOfWork.RefundCases.GetForExportAsync(status, adminId, request.FromDate, request.ToDate, cancellationToken);
            if (refunds.Count == 0)
                throw new ConflictException("Không có hồ sơ nào để xuất", ErrorCodes.RefundExportEmpty);

            var rows = refunds.Select(x => (x, ReadPayoutDetails(x))).ToList();
            foreach (var refund in refunds)
            {
                if (!request.ReExport)
                {
                    Transition(refund, RefundCaseStatuses.Approved, utcNow);
                    refund.ApprovedByAdminUserId = adminId;
                    refund.ApprovedAt = utcNow;
                    refund.RefundPayment.Status = PaymentStatuses.Pending;
                    await NotifyAsync(refund.CustomerUserId, "Hoàn tiền đang được xử lý", "Hồ sơ của bạn đã được chuyển cho kế toán.", refund, utcNow, cancellationToken);
                }

                await AuditAsync(adminId, "RefundExported", refund, utcNow, cancellationToken);
            }

            var content = RefundExportWorkbook.Build(rows);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return new RefundExportFileResponse($"hoan-tien-{utcNow:yyyyMMdd-HHmm}.xlsx", content);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<RefundCaseSummaryResponse> RejectAsync(
        Guid refundCaseId,
        RefundDecisionRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            EnsureNotOwnRefund(refund, currentUser.RequiredUserId);
            if (refund.Status != RefundCaseStatuses.Requested)
                throw new ConflictException("Chỉ từ chối được hồ sơ chưa xuất cho kế toán", ErrorCodes.RefundCaseNotRejectable);
            EnsureRowVersion(refund, request.ExpectedRowVersion);

            Transition(refund, RefundCaseStatuses.Rejected, utcNow);
            refund.RejectionReason = request.Reason.Trim();
            refund.RefundPayment.Status = PaymentStatuses.Cancelled;
            await AuditAsync(currentUser.RequiredUserId, "RefundCaseRejected", refund, utcNow, cancellationToken);
            await NotifyAsync(refund.CustomerUserId, "Hồ sơ hoàn tiền bị từ chối", "Vui lòng liên hệ hỗ trợ để biết thêm chi tiết.", refund, utcNow, cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

    public Task<RefundCaseSummaryResponse> CompleteAsync(
        Guid refundCaseId,
        CompleteRefundRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            var adminId = currentUser.RequiredUserId;
            EnsureNotOwnRefund(refund, adminId);
            if (refund.Status != RefundCaseStatuses.Approved)
                throw new ConflictException("Hồ sơ chưa được xuất cho kế toán", ErrorCodes.RefundCaseNotCompletable);
            EnsureRowVersion(refund, request.ExpectedRowVersion);

            var transactionId = string.IsNullOrWhiteSpace(request.ProviderTransactionId) ? null : request.ProviderTransactionId.Trim();
            if (transactionId is not null &&
                (await unitOfWork.RefundCases.ProviderTransactionUsedAsync(transactionId, cancellationToken) ||
                 await unitOfWork.Payments.ProviderTransactionExistsAsync(transactionId, cancellationToken)))
            {
                throw new ConflictException("Mã giao dịch đã được dùng cho hồ sơ khác", ErrorCodes.RefundTransactionDuplicated);
            }

            refund.ProviderTransactionId = transactionId;
            refund.RefundPayment.ProviderTransactionId = transactionId;
            Transition(refund, RefundCaseStatuses.Completed, utcNow);
            refund.CompletedAt = utcNow;
            refund.RefundPayment.Status = PaymentStatuses.Succeeded;
            refund.RefundPayment.ProcessedAt = utcNow;

            // Payment hiện tại chưa được lưu nên cộng tay vào tổng đã hoàn
            var refunded = refund.Amount + await unitOfWork.RefundCases.GetSucceededRefundTotalAsync(refund.OrderId, cancellationToken);
            var collected = await unitOfWork.TripBookings.GetSucceededPaymentTotalAsync(refund.OrderId, cancellationToken);
            refund.Order.PaymentStatus = refunded >= collected ? OrderPaymentStatuses.Refunded : OrderPaymentStatuses.PartiallyRefunded;
            refund.Order.UpdatedAt = utcNow;

            await AuditAsync(adminId, "RefundCompleted", refund, utcNow, cancellationToken);
            await NotifyAsync(refund.CustomerUserId, "Hoàn tiền hoàn tất", "Tiền hoàn đã được chuyển vào tài khoản của bạn.", refund, utcNow, cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

    public Task<RefundCaseSummaryResponse> MarkFailedAsync(
        Guid refundCaseId,
        RefundDecisionRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            EnsureNotOwnRefund(refund, currentUser.RequiredUserId);
            if (refund.Status != RefundCaseStatuses.Approved)
                throw new ConflictException("Hồ sơ chưa được xuất cho kế toán", ErrorCodes.RefundCaseNotCompletable);
            EnsureRowVersion(refund, request.ExpectedRowVersion);

            Transition(refund, RefundCaseStatuses.Failed, utcNow);
            refund.RejectionReason = request.Reason.Trim();
            refund.RefundPayment.Status = PaymentStatuses.Failed;
            refund.RefundPayment.ProcessedAt = utcNow;
            await AuditAsync(currentUser.RequiredUserId, "RefundMarkedFailed", refund, utcNow, cancellationToken);
            await NotifyAsync(refund.CustomerUserId, "Chuyển khoản hoàn tiền thất bại", "Vui lòng liên hệ hỗ trợ để được xử lý.", refund, utcNow, cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

    public async Task PurgeSensitiveDataAsync(CancellationToken cancellationToken = default)
    {
        var closedBefore = timeProvider.GetUtcNow() - PayoutRetention;
        var ids = await unitOfWork.RefundCases.GetPurgeableIdsAsync(closedBefore, cancellationToken);
        await ForEachCaseAsync(ids, id => MutateAsync(id, async (refund, utcNow) =>
        {
            refund.PayoutDetailsCiphertext = null;
            refund.PayoutDetailsNonce = null;
            refund.PayoutDetailsTag = null;
            refund.SensitiveDataPurgedAt = utcNow;
            refund.RowVersion++;
            await AuditAsync(null, "RefundSensitiveDataPurged", refund, utcNow, cancellationToken);
            return true;
        }, cancellationToken));
    }

    // Một hồ sơ lỗi không được chặn các hồ sơ còn lại của lượt job
    private async Task ForEachCaseAsync(IEnumerable<Guid> ids, Func<Guid, Task> action)
    {
        foreach (var id in ids)
        {
            try
            {
                await action(id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job hoàn tiền lỗi ở hồ sơ {RefundCaseId}", id);
            }
        }
    }

    private async Task<PagedResponse<RefundCaseQueueItemResponse>> GetQueueAsync(
        IReadOnlyList<string> queue,
        RefundCaseQueryRequest request,
        CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        string[] statuses = string.IsNullOrWhiteSpace(request.Status)
            ? [.. queue]
            : queue.Contains(request.Status) ? [request.Status] : [];

        var (items, totalItems) = await unitOfWork.RefundCases.SearchAsync(
            statuses,
            request.FromDate,
            request.ToDate,
            pageNumber,
            pageSize,
            cancellationToken);

        return new PagedResponse<RefundCaseQueueItemResponse>
        {
            Items = items.Select(MapQueueItem).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    private async Task<T> MutateAsync<T>(
        Guid refundCaseId,
        Func<RefundCase, DateTimeOffset, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var refund = await unitOfWork.RefundCases.GetForUpdateAsync(refundCaseId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ hoàn tiền", ErrorCodes.RefundCaseNotFound);

            var result = await action(refund, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task VerifyOtpAsync(Guid adminId, RefundExportRequest request, CancellationToken cancellationToken)
    {
        if (request.ChallengeId == Guid.Empty || string.IsNullOrWhiteSpace(request.VerificationCode))
            throw new BadRequestException("Cần nhập mã xác thực", ErrorCodes.RefundVerificationRequired);

        var error = await verificationService.VerifyAsync(
            adminId,
            request.ChallengeId,
            request.VerificationCode.Trim(),
            cancellationToken);
        if (error is null)
            return;

        // Commit trước khi báo lỗi, nếu không attempt_count bị rollback và giới hạn 5 lần vô hiệu
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        throw new BadRequestException(
            error == ErrorCodes.RefundVerificationExpired ? "Mã xác thực đã hết hạn" : "Mã xác thực không đúng",
            error);
    }

    private RefundPayoutDetails ReadPayoutDetails(RefundCase refund)
    {
        if (refund.PayoutDetailsCiphertext is null || refund.PayoutDetailsNonce is null || refund.PayoutDetailsTag is null)
            throw new ConflictException("Thông tin tài khoản đã được xóa theo thời hạn lưu trữ", ErrorCodes.RefundSensitiveDataPurged);

        return dataProtector.Unprotect(
            new ProtectedRefundPayload(refund.PayoutDetailsCiphertext, refund.PayoutDetailsNonce, refund.PayoutDetailsTag),
            refund.RefundCaseId,
            refund.OrderId);
    }

    private static void Transition(RefundCase refund, string status, DateTimeOffset utcNow)
    {
        refund.Status = status;
        refund.UpdatedAt = utcNow;
        refund.RowVersion++;
    }

    private static void EnsureRowVersion(RefundCase refund, int expectedRowVersion)
    {
        if (refund.RowVersion != expectedRowVersion)
            throw new ConflictException("Hồ sơ vừa được cập nhật, vui lòng tải lại", ErrorCodes.ConcurrencyConflict);
    }

    // Admin không được tự xử lý tiền hoàn của chính mình
    private static void EnsureNotOwnRefund(RefundCase refund, Guid actorId)
    {
        if (refund.CustomerUserId == actorId)
            throw new ForbiddenException("Không được xử lý hồ sơ hoàn tiền của chính mình");
    }

    // Trả 404 thay vì 403 để không lộ hồ sơ tồn tại
    private void EnsureOwned(RefundCase? refund)
    {
        if (refund is null || refund.CustomerUserId != currentUser.RequiredUserId)
            throw new NotFoundException("Không tìm thấy hồ sơ hoàn tiền", ErrorCodes.RefundCaseNotFound);
    }

    private Task AuditAsync(Guid? actorUserId, string action, RefundCase refund, DateTimeOffset utcNow, CancellationToken cancellationToken)
        => unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = EntityType,
            EntityId = refund.RefundCaseId,
            // Chỉ ID, trạng thái, số tiền, last4, mã giao dịch; không bao giờ có số tài khoản đầy đủ
            Details = JsonSerializer.Serialize(new
            {
                refund.OrderId,
                refund.Status,
                refund.Amount,
                refund.BankAccountLast4,
                refund.ProviderTransactionId
            }),
            CreatedAt = utcNow.UtcDateTime
        }, cancellationToken);

    private Task NotifyAsync(Guid recipientUserId, string title, string content, RefundCase refund, DateTimeOffset utcNow, CancellationToken cancellationToken)
        => unitOfWork.Notifications.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            NotificationType = EntityType,
            Title = title,
            Content = content,
            CreatedAt = utcNow.UtcDateTime,
            RelatedEntityType = EntityType,
            RelatedEntityId = refund.RefundCaseId
        }, cancellationToken);

    private async Task NotifyRolesAsync(
        IReadOnlyCollection<string> roles,
        string title,
        string content,
        RefundCase refund,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        foreach (var userId in await unitOfWork.RefundCases.GetActiveUserIdsByRolesAsync(roles, cancellationToken))
            await NotifyAsync(userId, title, content, refund, utcNow, cancellationToken);
    }

    private static string Mask(RefundCase refund) => $"******{refund.BankAccountLast4}";

    private static RefundCaseSummaryResponse MapSummary(RefundCase refund)
        => new()
        {
            RefundCaseId = refund.RefundCaseId,
            Amount = refund.Amount,
            Status = refund.Status,
            BankCode = refund.BankCode,
            MaskedAccount = Mask(refund),
            RowVersion = refund.RowVersion
        };

    private static RefundCaseResponse MapCustomer(RefundCase refund)
        => new()
        {
            RefundCaseId = refund.RefundCaseId,
            OrderId = refund.OrderId,
            OrderCode = refund.Order.OrderCode,
            Amount = refund.Amount,
            Status = refund.Status,
            BankCode = refund.BankCode,
            MaskedAccount = Mask(refund),
            Timeline = BuildTimeline(refund),
            CompletedAt = refund.CompletedAt
        };

    private static IReadOnlyList<RefundTimelineEventResponse> BuildTimeline(RefundCase refund)
    {
        (string Status, DateTimeOffset? At)[] events =
        [
            (RefundCaseStatuses.Requested, refund.CreatedAt),
            (RefundCaseStatuses.Approved, refund.ApprovedAt),
            (RefundCaseStatuses.Completed, refund.CompletedAt),
            (refund.Status, refund.UpdatedAt)
        ];

        return events
            .Where(x => x.At is not null)
            .DistinctBy(x => x.Status)
            .OrderBy(x => x.At)
            .Select(x => new RefundTimelineEventResponse { Status = x.Status, OccurredAt = x.At!.Value })
            .ToList();
    }

    private static RefundCaseQueueItemResponse MapQueueItem(RefundCase refund)
    {
        var trip = refund.Order.TripBookingOrder?.Trip;
        return new RefundCaseQueueItemResponse
        {
            RefundCaseId = refund.RefundCaseId,
            OrderId = refund.OrderId,
            OrderCode = refund.Order.OrderCode,
            TourName = trip?.Tour?.Name,
            TripStartAt = trip?.StartAt,
            CustomerName = refund.CustomerUser.FullName,
            OrderTotalAmount = refund.Order.TotalAmount,
            Amount = refund.Amount,
            Status = refund.Status,
            BankCode = refund.BankCode,
            MaskedAccount = Mask(refund),
            PolicyName = refund.Order.RefundPolicy.Name,
            PolicyVersion = refund.Order.RefundPolicy.Version,
            MinHoursBefore = refund.RefundPolicyRule?.MinHoursBefore,
            RefundPercentage = refund.RefundPolicyRule?.RefundPercentage,
            CancelReason = refund.Order.CancelReason,
            ProviderTransactionId = refund.ProviderTransactionId,
            CreatedAt = refund.CreatedAt,
            RowVersion = refund.RowVersion
        };
    }
}
