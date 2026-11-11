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

    Task<RefundCaseSummaryResponse?> GetSummaryByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    // Customer
    Task<RefundCaseResponse> GetAsync(Guid refundCaseId, CancellationToken cancellationToken = default);

    // Admin
    Task<PagedResponse<RefundCaseQueueItemResponse>> GetAdminQueueAsync(RefundCaseQueryRequest request, CancellationToken cancellationToken = default);
    Task<RefundChallengeResponse> RequestExportCodeAsync(CancellationToken cancellationToken = default);
    Task<RefundExportFileResponse> ExportAsync(RefundExportRequest request, CancellationToken cancellationToken = default);
    Task<RefundCaseSummaryResponse> RejectAsync(Guid refundCaseId, RefundDecisionRequest request, CancellationToken cancellationToken = default);
    Task<RefundCaseSummaryResponse> CompleteAsync(Guid refundCaseId, CompleteRefundRequest request, CancellationToken cancellationToken = default);
    Task<RefundCaseSummaryResponse> MarkFailedAsync(Guid refundCaseId, RefundDecisionRequest request, CancellationToken cancellationToken = default);

    // Job xóa dữ liệu hết hạn
    Task PurgeSensitiveDataAsync(CancellationToken cancellationToken = default);
}
