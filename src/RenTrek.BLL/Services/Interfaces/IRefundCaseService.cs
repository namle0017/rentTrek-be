using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IRefundCaseService
{
    // Dùng trong luồng hủy booking
    Task<RefundQuoteResult> QuoteAsync(RefundOrderInput order, DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    /// <summary>Tạo Payment Refund + RefundCase trong transaction của caller; không tự commit.</summary>
    Task<RefundCaseSummaryResponse> OpenForCancellationAsync(
        RefundOrderInput order,
        RefundQuoteResult quote,
        RefundBankAccountRequest bankAccount,
        string consentVersion,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>Khách nhập STK cho hồ sơ đang chờ (AwaitingBankInfo → Requested).</summary>
    Task<RefundCaseSummaryResponse> SubmitBankInfoAsync(Guid refundCaseId, SubmitRefundBankInfoRequest request, CancellationToken cancellationToken = default);

    Task<RefundCaseSummaryResponse?> GetSummaryByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    // Customer
    Task<RefundCaseResponse> GetAsync(Guid refundCaseId, CancellationToken cancellationToken = default);

    /// <summary>Danh sách hồ sơ hoàn tiền của khách đang đăng nhập.</summary>
    Task<PagedResponse<RefundCaseResponse>> GetMineAsync(RefundCaseQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Khách gửi lại yêu cầu hoàn tiền (kèm STK mới) cho hồ sơ bị từ chối / thất bại.</summary>
    Task<RefundCaseSummaryResponse> ResubmitAsync(Guid refundCaseId, SubmitRefundBankInfoRequest request, CancellationToken cancellationToken = default);

    // Admin
    Task<PagedResponse<RefundCaseQueueItemResponse>> GetAdminQueueAsync(RefundCaseQueryRequest request, CancellationToken cancellationToken = default);
    Task<RefundChallengeResponse> RequestExportCodeAsync(CancellationToken cancellationToken = default);
    Task<RefundExportFileResponse> ExportAsync(RefundExportRequest request, CancellationToken cancellationToken = default);
    Task<RefundCaseSummaryResponse> RejectAsync(Guid refundCaseId, RefundDecisionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Hoàn tất hồ sơ kèm 1..3 ảnh chứng từ bắt buộc (JPG/PNG/WebP, tối đa 5MB mỗi ảnh).</summary>
    Task<RefundCaseSummaryResponse> CompleteAsync(
        Guid refundCaseId,
        CompleteRefundRequest request,
        IReadOnlyList<ProofFileInput> files,
        CancellationToken cancellationToken = default);

    /// <summary>Nhập file Excel kết quả chuyển khoản của kế toán; dryRun chỉ kiểm tra, không ghi gì.</summary>
    Task<RefundImportResultResponse> ImportResultAsync(Stream file, string fileName, bool dryRun, CancellationToken cancellationToken = default);

    /// <summary>Mở ảnh chứng từ: Admin xem mọi hồ sơ; khách chỉ xem hồ sơ của mình đã Completed (còn lại 404).</summary>
    Task<(Stream Content, string ContentType, string FileName)> OpenProofAsync(
        Guid refundCaseId,
        Guid proofId,
        bool asAdmin,
        CancellationToken cancellationToken = default);
    Task<RefundCaseSummaryResponse> MarkFailedAsync(Guid refundCaseId, RefundDecisionRequest request, CancellationToken cancellationToken = default);

    // Job xóa dữ liệu hết hạn
    Task PurgeSensitiveDataAsync(CancellationToken cancellationToken = default);
}
