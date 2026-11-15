using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
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
    ILogger<RefundCaseService> logger,
    INotificationService notificationService,
    IPrivateFileStorage? privateStorage = null,
    IOptions<RefundSettings>? refundOptions = null) : IRefundCaseService, IRentalRefundOpener
{
    private const string PaymentTypeRefund = "Refund";
    private const string PaymentMethodBankTransfer = "BankTransfer";
    private const string PurposeTourFee = "TourFee";
    private const string PurposeRentalFee = "RentalFee";
    private const string EntityType = "RefundCase";
    private const int MaxPageSize = 100;
    private const long MaxImportBytes = 20 * 1024 * 1024;
    private const int MaxFailureReasonLength = 500;
    private static readonly string[] ExportableStatuses = [RefundCaseStatuses.Approved];
    private const int MaxAppealReasonLength = 1000;
    private const int MaxResubmits = 1;
    private static readonly TimeSpan ResubmitWindow = TimeSpan.FromDays(7);
    private const string ImportActionComplete = "Complete";
    private const string ImportActionFail = "Fail";
    private const string ImportActionSkip = "Skip";

    private sealed record ImportContext(Guid BatchId, string FileName, int RowNumber, int TotalRows);

    private static readonly string[] AdminOnly = [RoleNames.Admin, RoleNames.Staff];
    private TimeSpan PayoutRetention => TimeSpan.FromDays(refundOptions?.Value.PayoutRetentionDays ?? 30);

    public async Task<RefundQuoteResult> QuoteAsync(RefundOrderInput order, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var policy = await unitOfWork.RefundCases.GetPolicyWithRulesAsync(order.RefundPolicyId, cancellationToken);
        var paidAmount = order.PaymentStatus == OrderPaymentStatuses.Paid
            ? await unitOfWork.TripBookings.GetSucceededPaymentTotalAsync(order.OrderId, cancellationToken)
            : 0m;
        // Đổi lịch sang chuyến rẻ hơn đã hoàn phần chênh: không hoàn lại lần nữa khi hủy
        if (order.AmountCap > 0)
            paidAmount = Math.Min(paidAmount, order.AmountCap);
        // Đã hủy bớt hành khách: chỉ còn phần tiền của số ghế còn lại (cùng UnitPrice)
        if (order.TotalParticipants > 0 && order.ActiveParticipants < order.TotalParticipants)
            paidAmount = Math.Floor(paidAmount * order.ActiveParticipants / order.TotalParticipants);

        var hoursBefore = (order.TripStartAt - utcNow).TotalHours;
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
        RefundOrderInput order,
        RefundQuoteResult quote,
        RefundBankAccountRequest bankAccount,
        string consentVersion,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default,
        string source = RefundCaseSources.TripCancellation)
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
            Source = source,
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
        await NotifyRolesAsync(AdminOnly, "Hồ sơ hoàn tiền mới", $"Đơn {order.OrderCode} chờ duyệt.", refund, utcNow, cancellationToken);
        return MapSummary(refund);
    }

    public async Task<RefundCaseSummaryResponse> OpenRentalRefundAsync(
        Order order,
        decimal amount,
        string source,
        RefundBankAccountRequest? bankAccount,
        string? consentVersion,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (amount <= 0)
            throw new ConflictException("Số tiền hoàn phải lớn hơn 0", ErrorCodes.ValidationError);

        var originalPaymentId = await unitOfWork.RefundCases.GetLatestCollectionPaymentIdAsync(order.OrderId, cancellationToken);
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            OriginalPaymentId = originalPaymentId,
            PaymentType = PaymentTypeRefund,
            Amount = amount,
            PaymentMethod = PaymentMethodBankTransfer,
            Status = PaymentStatuses.PendingApproval,
            CreatedAt = utcNow,
            Purpose = source is RefundCaseSources.TripCancellation or RefundCaseSources.TripReschedule ? PurposeTourFee : PurposeRentalFee
        };
        await unitOfWork.Payments.AddAsync(payment, cancellationToken);

        var refund = new RefundCase
        {
            RefundCaseId = Guid.NewGuid(),
            OrderId = order.OrderId,
            RefundPaymentId = payment.PaymentId,
            CustomerUserId = order.CustomerUserId,
            Amount = amount,
            Source = source,
            Status = RefundCaseStatuses.AwaitingBankInfo,
            BankInfoRequestedAt = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        if (bankAccount is not null)
        {
            ApplyBankAccount(refund, bankAccount, consentVersion ?? RefundBankCodes.ConsentVersion, utcNow);
        }

        await unitOfWork.RefundCases.AddAsync(refund, cancellationToken);
        await AuditAsync(order.CustomerUserId, "RefundCaseCreated", refund, utcNow, cancellationToken);
        if (refund.Status == RefundCaseStatuses.Requested)
        {
            await NotifyRolesAsync(AdminOnly, "Hồ sơ hoàn tiền mới", $"Đơn {order.OrderCode} chờ duyệt.", refund, utcNow, cancellationToken);
        }
        else
        {
            await notificationService.QueueAsync(
                order.CustomerUserId,
                NotificationTypes.RefundBankInfoRequired,
                "Cần nhập tài khoản nhận hoàn tiền",
                $"Đơn {order.OrderCode} có {amount:N0}đ cần hoàn. Vui lòng nhập số tài khoản để hệ thống chuyển tiền.",
                EntityType,
                refund.RefundCaseId,
                $"RefundBankInfoRequired:{refund.RefundCaseId}",
                cancellationToken);
        }

        return MapSummary(refund);
    }

    public Task<RefundCaseSummaryResponse> SubmitBankInfoAsync(
        Guid refundCaseId,
        SubmitRefundBankInfoRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            EnsureOwned(refund);
            if (refund.Status != RefundCaseStatuses.AwaitingBankInfo)
                throw new ConflictException("Hồ sơ không ở bước chờ nhập tài khoản", ErrorCodes.RefundBankInfoNotRequired);

            var errors = new Dictionary<string, string[]>();
            if (request.BankAccount is null)
                errors["bankAccount"] = ["Cần cung cấp tài khoản nhận hoàn"];
            if (!request.ConsentAccepted || request.ConsentVersion != RefundBankCodes.ConsentVersion)
                errors["consentAccepted"] = ["Bạn cần đồng ý xử lý dữ liệu ngân hàng"];
            if (errors.Count > 0)
                throw new ValidationException(errors);

            ApplyBankAccount(refund, request.BankAccount!, RefundBankCodes.ConsentVersion, utcNow);
            refund.RejectionReason = null;
            refund.RowVersion++;
            await AuditAsync(refund.CustomerUserId, "RefundBankInfoSubmitted", refund, utcNow, cancellationToken);
            await NotifyRolesAsync(AdminOnly, "Hồ sơ hoàn tiền mới", $"Đơn {refund.Order.OrderCode} đã có STK, chờ duyệt.", refund, utcNow, cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

    /// <summary>Mã hoá STK + chủ tài khoản, chỉ lưu ngân hàng và 4 số cuối dạng rõ; chuyển hồ sơ sang Requested.</summary>
    private void ApplyBankAccount(RefundCase refund, RefundBankAccountRequest bankAccount, string consentVersion, DateTimeOffset utcNow)
    {
        var accountNumber = (bankAccount.AccountNumber ?? string.Empty).Trim();
        var holder = Regex.Replace((bankAccount.AccountHolder ?? string.Empty).Trim(), @"\s+", " ").ToUpperInvariant();
        var errors = new Dictionary<string, string[]>();
        if (!RefundBankCodes.All.Contains(bankAccount.BankCode))
            errors["bankAccount.bankCode"] = ["Ngân hàng không được hỗ trợ"];
        if (!Regex.IsMatch(accountNumber, @"^\d{6,20}$"))
            errors["bankAccount.accountNumber"] = ["Số tài khoản gồm 6–20 chữ số"];
        if (holder.Length is < 2 or > 100)
            errors["bankAccount.accountHolder"] = ["Tên chủ tài khoản gồm 2–100 ký tự"];
        if (errors.Count > 0)
            throw new ValidationException(errors);

        refund.BankCode = bankAccount.BankCode;
        refund.BankAccountLast4 = accountNumber[^4..];
        refund.ConsentVersion = consentVersion;
        refund.ConsentAcceptedAt = utcNow;
        var protectedPayload = dataProtector.Protect(new RefundPayoutDetails(accountNumber, holder), refund.RefundCaseId, refund.OrderId);
        refund.PayoutDetailsCiphertext = protectedPayload.Ciphertext;
        refund.PayoutDetailsNonce = protectedPayload.Nonce;
        refund.PayoutDetailsTag = protectedPayload.Tag;
        refund.BankInfoSubmittedAt = utcNow;
        refund.Status = RefundCaseStatuses.Requested;
        refund.UpdatedAt = utcNow;
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
        return MapCustomer(refund!, timeProvider.GetUtcNow());
    }

    public async Task<PagedResponse<RefundCaseResponse>> GetMineAsync(
        RefundCaseQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        IReadOnlyCollection<string> statuses = string.IsNullOrWhiteSpace(request.Status)
            ? []
            : request.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (items, total) = await unitOfWork.RefundCases.SearchByCustomerAsync(
            currentUser.RequiredUserId, statuses, pageNumber, pageSize, cancellationToken);
        return new PagedResponse<RefundCaseResponse>
        {
            Items = items.Select(x => MapCustomer(x, timeProvider.GetUtcNow())).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    /// <summary>
    /// Hồ sơ bị từ chối (vd. chưa có STK) hoặc thất bại: khách nhập STK mới và gửi lại. Hồ sơ quay về Requested,
    /// vào lần xuất Excel tiếp theo; admin vẫn có thể từ chối lại.
    /// </summary>
    public Task<RefundCaseSummaryResponse> ResubmitAsync(
        Guid refundCaseId,
        SubmitRefundBankInfoRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            EnsureOwned(refund);
            if (refund.Status is not (RefundCaseStatuses.Rejected or RefundCaseStatuses.Failed))
                throw new ConflictException("Chỉ gửi lại được hồ sơ bị từ chối hoặc thất bại", ErrorCodes.RefundCaseNotResubmittable);
            var isAppeal = refund.Status == RefundCaseStatuses.Rejected;
            if (isAppeal && !CanAppeal(refund, utcNow))
                throw new ConflictException("Hồ sơ đã hết lượt hoặc quá hạn 7 ngày để khiếu nại", ErrorCodes.RefundCaseNotResubmittable);
            if (await unitOfWork.RefundCases.HasOtherOpenCaseAsync(refund.OrderId, refund.RefundCaseId, cancellationToken))
                throw new ConflictException("Đơn này đang có hồ sơ hoàn tiền khác đang xử lý", ErrorCodes.RefundCaseNotResubmittable);

            var errors = new Dictionary<string, string[]>();
            if (request.BankAccount is null)
                errors["bankAccount"] = ["Cần cung cấp tài khoản nhận hoàn"];
            if (!request.ConsentAccepted || request.ConsentVersion != RefundBankCodes.ConsentVersion)
                errors["consentAccepted"] = ["Bạn cần đồng ý xử lý dữ liệu ngân hàng"];
            var appealReason = request.Reason?.Trim();
            if (isAppeal && string.IsNullOrEmpty(appealReason))
                errors["reason"] = ["Vui lòng nhập lý do khiếu nại"];
            else if (appealReason?.Length > MaxAppealReasonLength)
                errors["reason"] = [$"Lý do khiếu nại tối đa {MaxAppealReasonLength} ký tự"];
            if (errors.Count > 0)
                throw new ValidationException(errors);

            if (isAppeal)
            {
                refund.ResubmitCount++;
                refund.AppealReason = appealReason;
            }

            var previousStatus = refund.Status;
            var previousReason = refund.RejectionReason;
            ApplyBankAccount(refund, request.BankAccount!, RefundBankCodes.ConsentVersion, utcNow);
            refund.SensitiveDataPurgedAt = null;
            refund.RejectionReason = null;
            refund.ApprovedAt = null;
            refund.ApprovedByAdminUserId = null;
            refund.ExportedAt = null;
            refund.ProviderTransactionId = null;
            refund.CompletedAt = null;
            refund.RefundPayment.Status = PaymentStatuses.PendingApproval;
            refund.RefundPayment.ProviderTransactionId = null;
            refund.RefundPayment.ProcessedAt = null;
            refund.RowVersion++;
            await AuditAsync(refund.CustomerUserId, "RefundResubmitted", refund, utcNow, cancellationToken);
            await NotifyRolesAsync(
                AdminOnly,
                "Khách gửi lại yêu cầu hoàn tiền",
                $"Đơn {refund.Order.OrderCode} ({previousStatus}: {previousReason}) đã có STK mới, chờ duyệt.",
                refund,
                utcNow,
                cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

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

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        RefundChallengeResponse challenge;
        string code;
        try
        {
            (challenge, code) = await verificationService.IssueAsync(adminId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        // Gửi sau khi lưu để lỗi SMTP không làm mất giới hạn số lần gửi
        await emailSender.SendAsync(
            admin.Email,
            admin.FullName,
            "Mã xác thực hoàn tiền RenTrekk",
            $"<p>Mã xác thực của bạn là <strong>{code}</strong>. Mã có hiệu lực trong 5 phút.</p>",
            cancellationToken);
        return challenge;
    }

    public async Task<RefundExportFileResponse> ExportAsync(RefundExportRequest request, CancellationToken cancellationToken = default)
    {
        var adminId = currentUser.RequiredUserId;
        var utcNow = timeProvider.GetUtcNow();

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await VerifyOtpAsync(adminId, request, cancellationToken);

            // Chỉ hồ sơ đã duyệt (Approved), gồm cả hồ sơ đã gửi kế toán nhưng chưa có kết quả
            var refunds = await unitOfWork.RefundCases.GetForExportAsync(
                ExportableStatuses, adminId, request.FromDate, request.ToDate, cancellationToken);
            if (refunds.Count == 0)
                throw new ConflictException(
                    "Chưa có hồ sơ đã duyệt để xuất cho kế toán. Hãy duyệt hồ sơ đang chờ trước khi xuất.",
                    ErrorCodes.RefundExportEmpty);

            var rows = refunds.Select(x => (x, ReadPayoutDetails(x))).ToList();
            foreach (var refund in refunds)
            {
                if (refund.ExportedAt is null)
                {
                    refund.ExportedAt = utcNow;
                    refund.UpdatedAt = utcNow;
                    refund.RowVersion++;
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

    /// <summary>Admin/Staff duyệt hồ sơ (Requested → Approved); chỉ hồ sơ Approved mới được xuất cho kế toán.</summary>
    public Task<RefundCaseSummaryResponse> ApproveAsync(
        Guid refundCaseId,
        RefundApproveRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var actorId = currentUser.RequiredUserId;
            EnsureNotOwnRefund(refund, actorId);
            if (refund.Status != RefundCaseStatuses.Requested)
                throw new ConflictException("Chỉ duyệt được hồ sơ đang chờ duyệt", ErrorCodes.RefundCaseNotApprovable);
            EnsureRowVersion(refund, request.ExpectedRowVersion);

            Transition(refund, RefundCaseStatuses.Approved, utcNow);
            refund.ApprovedByAdminUserId = actorId;
            refund.ApprovedAt = utcNow;
            refund.ExportedAt = null;
            refund.RefundPayment.Status = PaymentStatuses.Pending;
            await AuditAsync(actorId, "RefundCaseApproved", refund, utcNow, cancellationToken);
            await NotifyAsync(
                refund.CustomerUserId,
                "Hồ sơ hoàn tiền đã được duyệt",
                $"Hồ sơ hoàn tiền cho đơn {refund.Order.OrderCode} đã được duyệt, đang chờ chuyển khoản.",
                refund,
                utcNow,
                cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

    public Task<RefundCaseSummaryResponse> RejectAsync(
        Guid refundCaseId,
        RefundDecisionRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            EnsureNotOwnRefund(refund, currentUser.RequiredUserId);
            if (refund.Status is not (RefundCaseStatuses.Requested or RefundCaseStatuses.AwaitingBankInfo))
                throw new ConflictException("Chỉ từ chối được hồ sơ chưa xuất cho kế toán", ErrorCodes.RefundCaseNotRejectable);
            EnsureRowVersion(refund, request.ExpectedRowVersion);

            Transition(refund, RefundCaseStatuses.Rejected, utcNow);
            refund.RejectionReason = request.Reason.Trim();
            refund.RejectedAt = utcNow;
            refund.RefundPayment.Status = PaymentStatuses.Cancelled;
            await AuditAsync(currentUser.RequiredUserId, "RefundCaseRejected", refund, utcNow, cancellationToken);
            await notificationService.QueueAsync(
                refund.CustomerUserId,
                NotificationTypes.RefundResult,
                "Hồ sơ hoàn tiền bị từ chối",
                $"Hồ sơ hoàn tiền cho đơn {refund.Order.OrderCode} bị từ chối. Vui lòng liên hệ hỗ trợ để biết thêm chi tiết.",
                EntityType,
                refund.RefundCaseId,
                $"RefundResult:{refund.RefundCaseId}:{refund.Status}",
                cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

    public async Task<RefundCaseSummaryResponse> CompleteAsync(
        Guid refundCaseId,
        CompleteRefundRequest request,
        IReadOnlyList<ProofFileInput> files,
        CancellationToken cancellationToken = default)
    {
        var proofs = await RefundProofFiles.ReadAsync(files, cancellationToken);
        return await CompleteCoreAsync(
            refundCaseId, request.ProviderTransactionId, request.ExpectedRowVersion, proofs, null, cancellationToken);
    }

    /// <summary>
    /// Lõi hoàn tất hồ sơ dùng chung cho thao tác thủ công và nhập Excel. Ảnh được upload trước transaction;
    /// transaction lỗi thì xóa file vừa upload. expectedRowVersion null = dùng rowVersion hiện tại (trạng thái Approved đóng vai trò khóa).
    /// </summary>
    private async Task<RefundCaseSummaryResponse> CompleteCoreAsync(
        Guid refundCaseId,
        string? providerTransactionId,
        int? expectedRowVersion,
        IReadOnlyList<RefundProofFile> proofs,
        ImportContext? import,
        CancellationToken cancellationToken)
    {
        var storage = privateStorage ?? throw new InvalidOperationException("Chưa cấu hình storage chứng từ");
        var uploaded = new List<(string Path, RefundProofFile File)>();
        try
        {
            foreach (var proof in proofs)
            {
                using var content = new MemoryStream(proof.Data);
                uploaded.Add((await storage.SaveAsync(content, proof.FileName, proof.ContentType, cancellationToken), proof));
            }

            return await MutateAsync(refundCaseId, async (refund, utcNow) =>
            {
                var adminId = currentUser.RequiredUserId;
                EnsureNotOwnRefund(refund, adminId);
                if (refund.Status != RefundCaseStatuses.Approved)
                    throw new ConflictException("Hồ sơ chưa được xuất cho kế toán", ErrorCodes.RefundCaseNotCompletable);
                if (expectedRowVersion is { } version)
                    EnsureRowVersion(refund, version);

                await unitOfWork.Payments.AcquireOrderLockAsync(refund.OrderId, cancellationToken);
                await unitOfWork.RefundCases.ReloadOrderAsync(refund.Order, cancellationToken);
                if (refund.Order.PaymentStatus == OrderPaymentStatuses.NeedsReconciliation)
                {
                    throw new ConflictException(
                        "Đơn đang chờ đối soát thanh toán",
                        ErrorCodes.OrderNotReconcilable);
                }

                if (string.IsNullOrWhiteSpace(providerTransactionId))
                    throw new BadRequestException("Cần nhập mã giao dịch ngân hàng");

                var transactionId = providerTransactionId.Trim();
                if (await unitOfWork.RefundCases.ProviderTransactionUsedAsync(transactionId, cancellationToken) ||
                    await unitOfWork.Payments.ProviderTransactionExistsAsync(transactionId, cancellationToken))
                {
                    throw new ConflictException("Mã giao dịch đã được dùng cho hồ sơ khác", ErrorCodes.RefundTransactionDuplicated);
                }

                refund.ProviderTransactionId = transactionId;
                refund.RefundPayment.ProviderTransactionId = transactionId;
                Transition(refund, RefundCaseStatuses.Completed, utcNow);
                refund.CompletedAt = utcNow;
                refund.RefundPayment.Status = PaymentStatuses.Succeeded;
                refund.RefundPayment.ProcessedAt = utcNow;

                await unitOfWork.RefundProofs.AddRangeAsync(
                    uploaded.Select(item => new RefundProof
                    {
                        RefundProofId = Guid.NewGuid(),
                        RefundCaseId = refund.RefundCaseId,
                        StoragePath = item.Path,
                        ContentType = item.File.ContentType,
                        FileName = item.File.FileName,
                        SizeBytes = item.File.Data.Length,
                        UploadedByUserId = adminId,
                        Source = import is null ? "Manual" : "ExcelImport",
                        CreatedAt = utcNow.UtcDateTime
                    }).ToList(),
                    cancellationToken);

                // Payment hiện tại chưa được lưu nên cộng tay vào tổng đã hoàn
                var refunded = refund.Amount + await unitOfWork.RefundCases.GetSucceededRefundTotalAsync(refund.OrderId, cancellationToken);
                var collected = await unitOfWork.TripBookings.GetSucceededPaymentTotalAsync(refund.OrderId, cancellationToken);
                refund.Order.PaymentStatus = refunded >= collected ? OrderPaymentStatuses.Refunded : OrderPaymentStatuses.PartiallyRefunded;
                refund.Order.UpdatedAt = utcNow;

                await AuditAsync(adminId, "RefundCompleted", refund, utcNow, cancellationToken, import, uploaded.Count);
                await notificationService.QueueAsync(
                    refund.CustomerUserId,
                    NotificationTypes.RefundResult,
                    "Hoàn tiền hoàn tất",
                    $"Tiền hoàn cho đơn {refund.Order.OrderCode} đã được chuyển thành công.",
                    EntityType,
                    refund.RefundCaseId,
                    $"RefundResult:{refund.RefundCaseId}:{refund.Status}",
                    cancellationToken);
                return MapSummary(refund);
            }, cancellationToken);
        }
        catch
        {
            foreach (var (path, _) in uploaded)
                await storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }
    }

    public Task<RefundCaseSummaryResponse> MarkFailedAsync(
        Guid refundCaseId,
        RefundDecisionRequest request,
        CancellationToken cancellationToken = default)
        => MarkFailedCoreAsync(refundCaseId, request.Reason, request.ExpectedRowVersion, null, cancellationToken);

    /// <summary>
    /// "Báo thất bại": chưa chuyển được tiền (sai STK, tài khoản đóng...). Hồ sơ quay về chờ khách nhập lại STK:
    /// xoá STK cũ, lưu lý do, thông báo cho khách. Nhận hồ sơ chờ xuất, đã gửi kế toán, hoặc đang chờ khách (nhắc lại).
    /// Từ file Excel kế toán chỉ nhận hồ sơ Approved (đã kiểm ở bước validate dòng).
    /// </summary>
    private Task<RefundCaseSummaryResponse> MarkFailedCoreAsync(
        Guid refundCaseId,
        string reason,
        int? expectedRowVersion,
        ImportContext? import,
        CancellationToken cancellationToken)
        => MutateAsync(refundCaseId, async (refund, utcNow) =>
        {
            EnsureNotOwnRefund(refund, currentUser.RequiredUserId);
            if (refund.Status is not (RefundCaseStatuses.Approved or RefundCaseStatuses.Requested or RefundCaseStatuses.AwaitingBankInfo))
                throw new ConflictException("Hồ sơ đã kết thúc, không thể báo thất bại", ErrorCodes.RefundCaseNotCompletable);
            if (import is not null && refund.Status != RefundCaseStatuses.Approved)
                throw new ConflictException("Hồ sơ chưa được xuất cho kế toán", ErrorCodes.RefundCaseNotCompletable);
            if (expectedRowVersion is { } version)
                EnsureRowVersion(refund, version);

            Transition(refund, RefundCaseStatuses.AwaitingBankInfo, utcNow);
            refund.RejectionReason = reason.Trim();
            // Xoá STK cũ để khách nhập lại; hồ sơ vào lại lần xuất Excel sau khi khách gửi STK mới
            refund.BankCode = null;
            refund.BankAccountLast4 = null;
            refund.PayoutDetailsCiphertext = null;
            refund.PayoutDetailsNonce = null;
            refund.PayoutDetailsTag = null;
            refund.BankInfoSubmittedAt = null;
            refund.BankInfoRequestedAt = utcNow;
            refund.ApprovedAt = null;
            refund.ApprovedByAdminUserId = null;
            refund.ExportedAt = null;
            refund.RefundPayment.Status = PaymentStatuses.PendingApproval;
            refund.RefundPayment.ProcessedAt = null;
            await AuditAsync(currentUser.RequiredUserId, "RefundMarkedFailed", refund, utcNow, cancellationToken, import);
            await notificationService.QueueAsync(
                refund.CustomerUserId,
                NotificationTypes.RefundBankInfoRequired,
                "Cần nhập lại tài khoản nhận hoàn tiền",
                $"Chưa chuyển được tiền hoàn cho đơn {refund.Order.OrderCode}: {refund.RejectionReason}. Vui lòng nhập lại số tài khoản để hệ thống chuyển tiền.",
                EntityType,
                refund.RefundCaseId,
                $"RefundBankInfoRequired:{refund.RefundCaseId}:{refund.RowVersion}",
                cancellationToken);
            return MapSummary(refund);
        }, cancellationToken);

    public async Task<(Stream Content, string ContentType, string FileName)> OpenProofAsync(
        Guid refundCaseId,
        Guid proofId,
        bool asAdmin,
        CancellationToken cancellationToken = default)
    {
        var proof = await unitOfWork.RefundProofs.GetByIdAsync(proofId, cancellationToken);
        if (proof is null || proof.RefundCaseId != refundCaseId)
            throw new NotFoundException("Không tìm thấy chứng từ", ErrorCodes.NotFound);

        if (!asAdmin)
        {
            // Khách chỉ thấy chứng từ của hồ sơ mình đã hoàn tất; mọi trường hợp khác trả 404
            var refund = await unitOfWork.RefundCases.GetDetailAsync(refundCaseId, cancellationToken);
            EnsureOwned(refund);
            if (refund!.Status != RefundCaseStatuses.Completed)
                throw new NotFoundException("Không tìm thấy chứng từ", ErrorCodes.NotFound);
        }

        var storage = privateStorage ?? throw new InvalidOperationException("Chưa cấu hình storage chứng từ");
        var stream = await storage.OpenReadAsync(proof.StoragePath, cancellationToken);
        return (stream, proof.ContentType, proof.FileName);
    }

    public async Task<RefundImportResultResponse> ImportResultAsync(
        Stream file,
        string fileName,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var adminId = currentUser.RequiredUserId;
        var safeName = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        if (!safeName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ nhận file Excel .xlsx");

        using var memory = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await file.ReadAsync(chunk, cancellationToken)) > 0)
        {
            memory.Write(chunk, 0, read);
            if (memory.Length > MaxImportBytes)
                throw new ValidationException("file", "File Excel tối đa 20MB");
        }

        if (memory.Length == 0)
            throw new ValidationException("file", "File Excel rỗng");

        memory.Position = 0;
        RefundResultWorkbook workbook;
        try
        {
            workbook = RefundResultWorkbookReader.Read(memory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not AppException)
        {
            logger.LogWarning(ex, "Không đọc được file kết quả hoàn tiền {FileName}", safeName);
            throw new BadRequestException("Không đọc được file Excel, vui lòng dùng đúng file đã xuất");
        }

        if (workbook.MissingHeaders.Count > 0)
        {
            throw new BadRequestException("File thiếu cột bắt buộc: " + string.Join(", ", workbook.MissingHeaders));
        }

        var result = new RefundImportResultResponse
        {
            BatchId = dryRun ? null : Guid.NewGuid(),
            FileName = safeName,
            DryRun = dryRun,
            TotalRows = workbook.Rows.Count
        };

        var plans = new List<(RefundResultRow Source, RefundImportRowResponse Row)>();
        var seenCases = new HashSet<Guid>();
        var seenTransactions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in workbook.Rows)
        {
            var row = await AnalyzeImportRowAsync(source, adminId, seenCases, seenTransactions, cancellationToken);
            plans.Add((source, row));
        }

        result.ValidRows = plans.Count(x => x.Row.Errors.Count == 0 && x.Row.Action != ImportActionSkip);

        foreach (var (source, row) in plans)
        {
            if (!dryRun && row.Errors.Count == 0 && row.Action != ImportActionSkip && row.RefundCaseId is { } id)
            {
                var import = new ImportContext(result.BatchId!.Value, safeName, source.RowNumber, workbook.Rows.Count);
                try
                {
                    if (row.Action == ImportActionComplete)
                    {
                        await CompleteCoreAsync(
                            id,
                            row.TransactionId,
                            null,
                            source.Images.Select(x => new RefundProofFile(
                                x.Data, RefundProofFiles.SafeName(x.FileName, x.ContentType), RefundProofFiles.Sniff(x.Data)!)).ToList(),
                            import,
                            cancellationToken);
                        row.ResultStatus = RefundCaseStatuses.Completed;
                    }
                    else
                    {
                        await MarkFailedCoreAsync(id, row.Reason!, null, import, cancellationToken);
                        row.ResultStatus = RefundCaseStatuses.AwaitingBankInfo;
                    }

                    row.Applied = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Mỗi dòng độc lập: dòng lỗi không rollback các dòng đã áp dụng
                    unitOfWork.ClearChangeTracker();
                    row.Applied = false;
                    row.ResultStatus = null;
                    row.Errors.Add(ex is AppException ? ex.Message : "Lỗi hệ thống khi xử lý dòng này");
                    if (ex is not AppException)
                        logger.LogError(ex, "Nhập kết quả hoàn tiền lỗi ở dòng {RowNumber}", source.RowNumber);
                }
            }

            result.Rows.Add(row);
        }

        result.ErrorCount = result.Rows.Count(x => x.Errors.Count > 0);
        if (dryRun)
        {
            result.CompletedCount = result.Rows.Count(x => x.Errors.Count == 0 && x.Action == ImportActionComplete);
            result.FailedCount = result.Rows.Count(x => x.Errors.Count == 0 && x.Action == ImportActionFail);
        }
        else
        {
            result.CompletedCount = result.Rows.Count(x => x.Applied && x.ResultStatus == RefundCaseStatuses.Completed);
            result.FailedCount = result.Rows.Count(x => x.Applied && x.ResultStatus == RefundCaseStatuses.AwaitingBankInfo);
        }

        return result;
    }

    private async Task<RefundImportRowResponse> AnalyzeImportRowAsync(
        RefundResultRow source,
        Guid adminId,
        HashSet<Guid> seenCases,
        HashSet<string> seenTransactions,
        CancellationToken cancellationToken)
    {
        var row = new RefundImportRowResponse
        {
            RowNumber = source.RowNumber,
            RefundCode = source.RefundCode,
            ImageCount = source.Images.Count,
            Action = ImportActionSkip
        };

        // Khớp hồ sơ: GUID ẩn trước, không có thì dùng mã hồ sơ và chỉ nhận khi khớp duy nhất
        var caseId = source.RefundCaseId;
        if (caseId is null && !string.IsNullOrWhiteSpace(source.RefundCode))
        {
            var matches = await unitOfWork.RefundCases.FindApprovedIdsByCodeAsync(source.RefundCode, cancellationToken);
            if (matches.Count == 1)
                caseId = matches[0];
            else
                row.Errors.Add(matches.Count > 1 ? "Mã hồ sơ trùng nhiều hồ sơ, cần cột RefundCaseId" : "Không tìm thấy hồ sơ theo mã hồ sơ");
        }
        else if (caseId is null)
        {
            row.Errors.Add("Không xác định được hồ sơ (thiếu RefundCaseId / Mã hồ sơ)");
        }

        RefundCase? refund = null;
        if (caseId is { } id)
        {
            row.RefundCaseId = id;
            refund = await unitOfWork.RefundCases.GetDetailAsync(id, cancellationToken);
            if (refund is null)
            {
                row.Errors.Add("Không tìm thấy hồ sơ hoàn tiền");
            }
            else
            {
                row.CustomerName = refund.CustomerUser?.FullName ?? string.Empty;
                row.Amount = refund.Amount;
                if (string.IsNullOrEmpty(row.RefundCode))
                    row.RefundCode = $"HT-{refund.RefundCaseId:N}"[..11].ToUpperInvariant();
                if (refund.Status != RefundCaseStatuses.Approved)
                    row.Errors.Add($"Hồ sơ không ở trạng thái Approved (hiện là {refund.Status})");
                if (refund.CustomerUserId == adminId)
                    row.Errors.Add("Không được xử lý hồ sơ hoàn tiền của chính mình");
                if (!seenCases.Add(id))
                    row.Errors.Add("Hồ sơ xuất hiện nhiều lần trong file");
            }
        }

        if (source.Transferred is null)
        {
            row.Errors.Add("Chưa điền trạng thái");
            return row;
        }

        if (row.Errors.Count > 0 && refund is null)
            return row;

        if (source.Transferred == true)
        {
            row.TransactionId = source.TransactionId.Trim();
            if (row.TransactionId.Length == 0)
                row.Errors.Add("Thiếu mã giao dịch");
            else if (row.TransactionId.Length > 255)
                row.Errors.Add("Mã giao dịch không vượt quá 255 ký tự");
            else if (!seenTransactions.Add(row.TransactionId))
                row.Errors.Add("Mã giao dịch bị trùng trong file");
            else if (await unitOfWork.RefundCases.ProviderTransactionUsedAsync(row.TransactionId, cancellationToken) ||
                     await unitOfWork.Payments.ProviderTransactionExistsAsync(row.TransactionId, cancellationToken))
                row.Errors.Add("Mã giao dịch đã được dùng cho hồ sơ khác");

            if (source.Images.Count == 0)
                row.Errors.Add("Thiếu hình ảnh minh chứng");
            else if (source.Images.Count > RefundProofFiles.MaxFiles)
                row.Errors.Add($"Tối đa {RefundProofFiles.MaxFiles} ảnh minh chứng mỗi dòng");
            else
            {
                for (var i = 0; i < source.Images.Count; i++)
                {
                    var error = RefundProofFiles.Validate(source.Images[i].Data, $"Ảnh {i + 1}");
                    if (error is not null)
                        row.Errors.Add(error);
                }
            }

            row.Action = row.Errors.Count == 0 ? ImportActionComplete : ImportActionSkip;
        }
        else
        {
            row.Reason = source.Reason.Trim();
            if (row.Reason.Length == 0)
                row.Errors.Add("Thiếu lý do chưa chuyển");
            else if (row.Reason.Length > MaxFailureReasonLength)
                row.Errors.Add($"Lý do tối đa {MaxFailureReasonLength} ký tự");
            row.Action = row.Errors.Count == 0 ? ImportActionFail : ImportActionSkip;
        }

        return row;
    }

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

    private Task AuditAsync(
        Guid? actorUserId,
        string action,
        RefundCase refund,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken,
        ImportContext? import = null,
        int proofCount = 0)
    {
        // Chỉ ID, trạng thái, số tiền, last4, mã giao dịch; không bao giờ có số tài khoản đầy đủ
        var details = new Dictionary<string, object?>
        {
            ["OrderId"] = refund.OrderId,
            ["Status"] = refund.Status,
            ["Amount"] = refund.Amount,
            ["BankAccountLast4"] = refund.BankAccountLast4,
            ["ProviderTransactionId"] = refund.ProviderTransactionId
        };
        if (proofCount > 0)
            details["ProofCount"] = proofCount;
        if (import is not null)
        {
            details["ImportBatchId"] = import.BatchId;
            details["ImportFileName"] = import.FileName;
            details["ImportRowNumber"] = import.RowNumber;
            details["ImportTotalRows"] = import.TotalRows;
        }

        return unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = EntityType,
            EntityId = refund.RefundCaseId,
            Details = JsonSerializer.Serialize(details),
            CreatedAt = utcNow.UtcDateTime
        }, cancellationToken);
    }

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

    private static string Mask(RefundCase refund)
        => string.IsNullOrEmpty(refund.BankAccountLast4) ? string.Empty : $"******{refund.BankAccountLast4}";

    private static RefundCaseSummaryResponse MapSummary(RefundCase refund)
        => new()
        {
            RefundCaseId = refund.RefundCaseId,
            Amount = refund.Amount,
            Status = refund.Status,
            BankCode = refund.BankCode,
            Source = refund.Source,
            MaskedAccount = Mask(refund),
            RowVersion = refund.RowVersion
        };

    // Khiếu nại (gửi lại hồ sơ Rejected): tối đa 1 lần, trong 7 ngày từ lúc bị từ chối
    private static bool CanAppeal(RefundCase refund, DateTimeOffset utcNow)
        => refund.ResubmitCount < MaxResubmits && (ResubmitDeadlineOf(refund) is not { } deadline || utcNow <= deadline);

    private static DateTimeOffset? ResubmitDeadlineOf(RefundCase refund)
        => refund.Status == RefundCaseStatuses.Rejected ? (refund.RejectedAt ?? refund.UpdatedAt) + ResubmitWindow : null;

    private static RefundCaseResponse MapCustomer(RefundCase refund, DateTimeOffset utcNow)
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
            CompletedAt = refund.CompletedAt,
            ProviderTransactionId = refund.Status == RefundCaseStatuses.Completed ? refund.ProviderTransactionId : null,
            // Khách chỉ thấy chứng từ khi hồ sơ đã hoàn tất
            Proofs = refund.Status == RefundCaseStatuses.Completed ? MapProofs(refund) : [],
            FailureReason = FailureReasonOf(refund),
            RejectionReason = refund.Status == RefundCaseStatuses.Rejected ? refund.RejectionReason : null,
            OrderType = refund.Order.OrderType,
            Source = refund.Source,
            TourName = refund.Order.TripBookingOrder?.Trip?.Tour?.Name,
            CreatedAt = refund.CreatedAt,
            UpdatedAt = refund.UpdatedAt,
            CanSubmitBankInfo = refund.Status == RefundCaseStatuses.AwaitingBankInfo,
            CanResubmit = refund.Status == RefundCaseStatuses.Failed
                || (refund.Status == RefundCaseStatuses.Rejected && CanAppeal(refund, utcNow)),
            ResubmitDeadline = refund.Status == RefundCaseStatuses.Rejected && refund.ResubmitCount < MaxResubmits
                ? ResubmitDeadlineOf(refund)
                : null,
            AppealReason = refund.AppealReason
        };

    private static List<RefundProofResponse> MapProofs(RefundCase refund)
        => refund.Proofs
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new RefundProofResponse
            {
                ProofId = x.RefundProofId,
                FileName = x.FileName,
                ContentType = x.ContentType,
                CreatedAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)
            })
            .ToList();

    private static string? FailureReasonOf(RefundCase refund)
        => refund.Status is RefundCaseStatuses.Failed or RefundCaseStatuses.AwaitingBankInfo ? refund.RejectionReason : null;

    private static List<RefundTimelineEventResponse> BuildTimeline(RefundCase refund)
    {
        (string Status, DateTimeOffset? At)[] events =
        [
            (RefundCaseStatuses.AwaitingBankInfo, refund.BankInfoRequestedAt),
            (RefundCaseStatuses.Requested, refund.BankInfoRequestedAt is null ? refund.CreatedAt : refund.BankInfoSubmittedAt),
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
            OrderType = refund.Order.OrderType,
            Source = refund.Source,
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
            AppealReason = refund.AppealReason,
            RejectionReason = refund.Status == RefundCaseStatuses.Rejected ? refund.RejectionReason : null,
            ApprovedAt = refund.ApprovedAt,
            ExportedAt = refund.ExportedAt,
            ProviderTransactionId = refund.ProviderTransactionId,
            CreatedAt = refund.CreatedAt,
            RowVersion = refund.RowVersion,
            FailureReason = FailureReasonOf(refund),
            Proofs = MapProofs(refund)
        };
    }
}
