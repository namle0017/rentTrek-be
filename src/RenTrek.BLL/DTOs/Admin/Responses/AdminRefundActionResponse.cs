namespace RenTrek.BLL.DTOs.Admin.Responses;

/// <summary>
/// Kết quả thao tác trên một khoản hoàn (duyệt, thử lại, đánh dấu hoàn thủ công).
/// </summary>
/// <param name="Amount">Số tiền hoàn, mang dấu âm.</param>
/// <param name="ProviderRefundId">Mã giao dịch hoàn (mã tham chiếu ngân hàng khi hoàn thủ công); null khi chưa chuyển tiền.</param>
public sealed record AdminRefundActionResponse(
    Guid PaymentId,
    string OrderCode,
    decimal Amount,
    string Status,
    string? ProviderRefundId);
