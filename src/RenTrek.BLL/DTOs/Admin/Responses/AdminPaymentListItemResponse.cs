namespace RenTrek.BLL.DTOs.Admin.Responses;

/// <summary>
/// Một dòng giao dịch thu / hoàn ở màn O27 (sổ giao dịch) và O28 (hàng đợi hoàn tiền &amp; đối soát).
/// </summary>
/// <param name="Amount">Số tiền; khoản hoàn mang dấu âm.</param>
/// <param name="ReceivedAmount">Số tiền thực nhận với khoản thu đã có mã giao dịch; null với khoản hoàn hoặc khoản thu chưa nhận tiền.</param>
/// <param name="FailureReason">Lý do thất bại / từ chối lấy từ hồ sơ hoàn tiền (nếu có).</param>
/// <param name="PolicyRuleDescription">Mô tả mốc chính sách hoàn áp dụng, ví dụ "Hủy trước 72 giờ · Hoàn 80%".</param>
/// <param name="HasRefundCase">Khoản hoàn đã có hồ sơ hoàn tiền: xử lý ở /admin/refund-cases thay vì các nút ở O28.</param>
public sealed record AdminPaymentListItemResponse(
    Guid PaymentId,
    Guid OrderId,
    string OrderCode,
    string OrderPaymentStatus,
    string PaymentType,
    string Purpose,
    decimal Amount,
    decimal? ReceivedAmount,
    string PaymentMethod,
    string? ProviderTransactionId,
    string Status,
    string? FailureReason,
    string? PolicyRuleDescription,
    bool HasRefundCase,
    DateTimeOffset CreatedAt);
