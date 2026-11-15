using System.Globalization;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Sổ giao dịch (O27), hàng đợi hoàn tiền (O28) và xử lý khoản hoàn theo luồng thủ công:
/// duyệt = chuyển kế toán (Pending), thử lại = đưa khoản Failed về Pending, đánh dấu thủ công = Succeeded kèm mã tham chiếu ngân hàng.
/// Khoản hoàn đã có hồ sơ hoàn tiền (refund case) đi theo luồng /admin/refund-cases, không xử lý ở đây.
/// </summary>
public sealed class AdminPaymentService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IPrivateFileStorage? privateStorage = null)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminPaymentService
{
    private const string Refund = "Refund";
    private const string Collection = "Collection";
    private const string RetryAction = "RefundRetry";
    private const int MaxRetries = 3;

    private static readonly string[] Statuses =
        [PaymentStatuses.PendingApproval, PaymentStatuses.Pending, PaymentStatuses.Succeeded, PaymentStatuses.Failed, PaymentStatuses.Cancelled];

    private static readonly string[] RefundTabs =
        [PaymentStatuses.PendingApproval, PaymentStatuses.Pending, PaymentStatuses.Failed, OrderPaymentStatuses.NeedsReconciliation, PaymentStatuses.Succeeded];

    public async Task<PagedResponse<AdminPaymentListItemResponse>> GetPaymentsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        CheckPaging(request);
        Check(request.Status == null || Statuses.Contains(request.Status), "status", "Trạng thái phải là một trong: " + string.Join(", ", Statuses));
        var page = await UnitOfWork.Payments.GetAdminPagedAsync(ToFilter(request, OrderScope, tab: null), cancellationToken);
        return await ToPagedWithProofsAsync(page, request, cancellationToken);
    }

    public async Task<AdminPaymentStatsResponse> GetPaymentStatsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        Check(request.Status == null || Statuses.Contains(request.Status), "status", "Trạng thái phải là một trong: " + string.Join(", ", Statuses));
        var row = await UnitOfWork.Payments.GetAdminStatsAsync(ToFilter(request, OrderScope, tab: null), cancellationToken);
        return new(row.TotalCollectionSucceeded, row.TotalRefundSucceeded,
            row.TotalCollectionSucceeded - row.TotalRefundSucceeded, row.PendingCount, row.PendingAmount);
    }

    public async Task<PagedResponse<AdminPaymentListItemResponse>> GetRefundsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        CheckPaging(request);
        var tab = request.Tab ?? PaymentStatuses.PendingApproval;
        Check(RefundTabs.Contains(tab), "tab", "Tab phải là một trong: " + string.Join(", ", RefundTabs));
        var page = await UnitOfWork.Payments.GetAdminPagedAsync(ToFilter(request, null, tab), cancellationToken);
        return await ToPagedWithProofsAsync(page, request, cancellationToken);
    }

    public Task<AdminRefundActionResponse> ApproveAsync(Guid paymentId, CancellationToken cancellationToken)
        => RefundActionAsync(paymentId, "RefundApproved", ErrorCodes.RefundNotApprovable, refund =>
        {
            if (refund.Status != PaymentStatuses.PendingApproval)
                throw new ConflictException(
                    "Khoản hoàn tiền không ở trạng thái Chờ duyệt (PendingApproval) hoặc đã được duyệt bởi người khác.",
                    ErrorCodes.RefundNotApprovable);

            // Không cho tổng hoàn (đã hoàn + đang chờ chuyển + khoản này) vượt tổng tiền đã thu của đơn
            var payments = refund.Order.Payments;
            var collected = payments.Where(x => x.PaymentType != Refund && x.Status == PaymentStatuses.Succeeded).Sum(x => x.Amount);
            var committed = payments.Where(x => x.PaymentType == Refund && x.PaymentId != refund.PaymentId
                && x.Status is PaymentStatuses.Pending or PaymentStatuses.Succeeded).Sum(x => x.Amount);
            if (refund.Amount > collected - committed)
                throw new ConflictException("Số tiền hoàn vượt tổng tiền đã thu của đơn", ErrorCodes.RefundExceedsCollected);

            refund.Status = PaymentStatuses.Pending;
            return Task.FromResult<object?>(new { refund.Amount, collected, committed });
        }, cancellationToken);

    public Task<AdminRefundActionResponse> RetryAsync(Guid paymentId, CancellationToken cancellationToken)
        => RefundActionAsync(paymentId, RetryAction, ErrorCodes.RefundNotRetryable, async refund =>
        {
            if (refund.Status != PaymentStatuses.Failed)
                throw new ConflictException("Chỉ thử lại khoản hoàn đang ở trạng thái Thất bại (Failed)", ErrorCodes.RefundNotRetryable);

            // ponytail: đếm số lần thử lại từ audit log; thêm cột retry_count nếu cần báo cáo/lọc theo số lần thử
            var retries = await UnitOfWork.AuditLogs.CountAsync(RetryAction, refund.PaymentId, cancellationToken);
            if (retries >= MaxRetries)
                throw new ConflictException(
                    $"Đã thử lại tối đa {MaxRetries} lần. Vui lòng hoàn thủ công (mark-manual).",
                    ErrorCodes.RefundNotRetryable);

            refund.Status = PaymentStatuses.Pending;
            refund.ProcessedAt = null;
            return (object?)new { attempt = retries + 1, maxRetries = MaxRetries };
        }, cancellationToken);

    public async Task<AdminRefundActionResponse> MarkManualAsync(
        Guid paymentId,
        MarkManualRefundRequest request,
        IReadOnlyList<ProofFileInput> files,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        var proofs = await RefundProofFiles.ReadAsync(files, cancellationToken);
        var storage = privateStorage ?? throw new InvalidOperationException("Chưa cấu hình storage chứng từ");
        var uploaded = new List<(string Path, RefundProofFile File)>();
        try
        {
            foreach (var proof in proofs)
            {
                using var content = new MemoryStream(proof.Data);
                uploaded.Add((await storage.SaveAsync(content, proof.FileName, proof.ContentType, cancellationToken), proof));
            }

            return await MarkManualCoreAsync(paymentId, request, uploaded, cancellationToken);
        }
        catch
        {
            foreach (var (path, _) in uploaded)
                await storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }
    }

    public async Task<(Stream Content, string ContentType, string FileName)> OpenRefundProofAsync(
        Guid paymentId,
        Guid proofId,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        var proof = await UnitOfWork.RefundProofs.GetByIdAsync(proofId, cancellationToken);
        if (proof is null || proof.PaymentId != paymentId)
            throw new NotFoundException("Không tìm thấy chứng từ", ErrorCodes.NotFound);
        var storage = privateStorage ?? throw new InvalidOperationException("Chưa cấu hình storage chứng từ");
        return (await storage.OpenReadAsync(proof.StoragePath, cancellationToken), proof.ContentType, proof.FileName);
    }

    private Task<AdminRefundActionResponse> MarkManualCoreAsync(
        Guid paymentId,
        MarkManualRefundRequest request,
        IReadOnlyList<(string Path, RefundProofFile File)> uploaded,
        CancellationToken cancellationToken)
    {
        var reference = request.ManualReference.Trim();
        var note = request.Note?.Trim();
        return RefundActionAsync(paymentId, "RefundMarkedManual", ErrorCodes.RefundNotCompletable, async refund =>
        {
            // Gọi lặp với cùng mã tham chiếu sau khi đã ghi nhận: trả kết quả cũ, không ghi audit lần nữa
            if (refund.Status == PaymentStatuses.Succeeded && refund.ProviderTransactionId == reference) return null;
            if (refund.Status is not (PaymentStatuses.Pending or PaymentStatuses.Failed))
                throw new ConflictException(
                    "Chỉ đánh dấu hoàn thủ công cho khoản hoàn đã duyệt (Pending) hoặc thất bại (Failed)",
                    ErrorCodes.RefundNotCompletable);

            var order = refund.Order;
            if (order.PaymentStatus == OrderPaymentStatuses.NeedsReconciliation)
                throw new ConflictException("Đơn đang chờ đối soát thanh toán", ErrorCodes.OrderNotReconcilable);

            // Khoá theo mã tham chiếu để hai khoản hoàn khác nhau không cùng ghi một mã giao dịch ngân hàng
            await UnitOfWork.AdminOperations.AcquireLockAsync("manual-reference:" + reference, cancellationToken);
            if (await UnitOfWork.RefundCases.ProviderTransactionUsedAsync(reference, cancellationToken) ||
                await UnitOfWork.Payments.ProviderTransactionExistsAsync(reference, cancellationToken))
                throw new ConflictException("Mã tham chiếu đã được dùng cho giao dịch khác", ErrorCodes.RefundTransactionDuplicated);

            refund.Status = PaymentStatuses.Succeeded;
            refund.ProviderTransactionId = reference;
            refund.ProcessedAt = Clock.GetUtcNow();
            foreach (var (path, file) in uploaded)
            {
                await UnitOfWork.RefundProofs.AddAsync(new RefundProof
                {
                    RefundProofId = Guid.NewGuid(),
                    PaymentId = refund.PaymentId,
                    StoragePath = path,
                    ContentType = file.ContentType,
                    FileName = file.FileName,
                    SizeBytes = file.Data.Length,
                    UploadedByUserId = ActorId,
                    Source = "Manual",
                    CreatedAt = Now
                }, cancellationToken);
            }

            // refund nằm trong order.Payments (cùng instance tracking) nên tổng đã gồm khoản vừa đổi trạng thái
            var refunded = order.Payments.Where(x => x.PaymentType == Refund && x.Status == PaymentStatuses.Succeeded).Sum(x => x.Amount);
            var collected = order.Payments.Where(x => x.PaymentType != Refund && x.Status == PaymentStatuses.Succeeded).Sum(x => x.Amount);
            order.PaymentStatus = refunded >= collected ? OrderPaymentStatuses.Refunded : OrderPaymentStatuses.PartiallyRefunded;
            return new { refund.Amount, reference, note, proofCount = uploaded.Count, orderPaymentStatus = order.PaymentStatus };
        }, cancellationToken);
    }

    /// <summary>
    /// Khung chung cho thao tác trên một khoản hoàn: khoá theo khoản hoàn rồi khoá thanh toán của đơn (cùng thứ tự với
    /// hồ sơ hoàn tiền / webhook), tải bản tracking, chặn khoản đã có hồ sơ hoàn tiền, chạy <paramref name="mutate"/> và ghi audit.
    /// <paramref name="mutate"/> trả null khi không có gì thay đổi (gọi lặp).
    /// </summary>
    private async Task<AdminRefundActionResponse> RefundActionAsync(
        Guid paymentId,
        string action,
        string refundCaseErrorCode,
        Func<Payment, Task<object?>> mutate,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        AdminRefundActionResponse? result = null;
        await AtomicAsync("refund:" + paymentId, async () =>
        {
            var orderId = await UnitOfWork.Payments.GetOrderIdAsync(paymentId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy khoản hoàn tiền", ErrorCodes.NotFound);
            await UnitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            var refund = await UnitOfWork.Payments.GetForUpdateAsync(paymentId, cancellationToken);
            if (refund is not { PaymentType: Refund })
                throw new NotFoundException("Không tìm thấy khoản hoàn tiền", ErrorCodes.NotFound);
            if (await UnitOfWork.Payments.HasRefundCaseAsync(paymentId, cancellationToken))
                throw new ConflictException(
                    "Khoản hoàn này đã có hồ sơ hoàn tiền, vui lòng xử lý tại mục Hồ sơ hoàn tiền (/admin/refund-cases)",
                    refundCaseErrorCode);

            var details = await mutate(refund);
            if (details != null)
            {
                refund.Order.UpdatedAt = Clock.GetUtcNow();
                await AuditAsync(action, "Payment", paymentId, details, cancellationToken);
            }

            result = new(refund.PaymentId, refund.Order.OrderCode, -refund.Amount, refund.Status,
                refund.Status == PaymentStatuses.Succeeded ? refund.ProviderTransactionId : null);
        }, cancellationToken);
        return result!;
    }

    private async Task<PagedResponse<AdminPaymentListItemResponse>> ToPagedWithProofsAsync(
        (IReadOnlyList<AdminPaymentRow> Items, int TotalCount) page,
        AdminQueryRequest request,
        CancellationToken cancellationToken)
    {
        var refundIds = page.Items.Where(x => x.PaymentType == Refund).Select(x => x.PaymentId).ToList();
        var proofs = (await UnitOfWork.RefundProofs.GetByPaymentIdsAsync(refundIds, cancellationToken))
            .GroupBy(x => x.PaymentId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RefundProofResponse>)g.Select(ToProof).ToList());
        return ToPaged(page, request, row => ToResponse(row, proofs.GetValueOrDefault(row.PaymentId) ?? []));
    }

    private static RefundProofResponse ToProof(RefundProof proof) => new()
    {
        ProofId = proof.RefundProofId,
        FileName = proof.FileName,
        ContentType = proof.ContentType,
        CreatedAt = DateTime.SpecifyKind(proof.CreatedAt, DateTimeKind.Utc)
    };

    private static AdminPaymentFilter ToFilter(AdminQueryRequest request, Guid? scopeUserId, string? tab) => new()
    {
        ScopeUserId = scopeUserId,
        // Tab hàng đợi O28 đã cố định loại giao dịch và trạng thái
        PaymentType = tab == null ? request.PaymentType : null,
        Status = tab == null ? request.Status : null,
        Tab = tab,
        Purpose = request.Purpose,
        FromDate = request.FromDate,
        ToDate = request.ToDate,
        Search = request.Search,
        SortBy = request.SortBy,
        IsDescending = IsDescending(request),
        PageNumber = request.PageNumber,
        PageSize = request.PageSize
    };

    private static AdminPaymentListItemResponse ToResponse(AdminPaymentRow x, IReadOnlyList<RefundProofResponse> proofs) => new(
        x.PaymentId, x.OrderId, x.OrderCode, x.OrderPaymentStatus, x.PaymentType, x.Purpose,
        x.PaymentType == Refund ? -x.Amount : x.Amount,
        x.PaymentType == Collection && x.ProviderTransactionId != null ? x.Amount : null,
        x.PaymentMethod, x.ProviderTransactionId, x.Status, x.FailureReason,
        x.PolicyMinHoursBefore is { } hours
            ? $"Hủy trước {hours} giờ · Hoàn {x.PolicyRefundPercentage?.ToString("0.##", CultureInfo.InvariantCulture)}%"
            : null,
        x.HasRefundCase, x.CreatedAt,
        x.PaymentType == Refund ? x.ProviderTransactionId : null,
        proofs);
}
