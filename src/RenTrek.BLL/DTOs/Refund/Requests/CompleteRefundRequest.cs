namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Admin ghi nhận kế toán đã chuyển khoản hoàn tiền.
/// </summary>
public sealed class CompleteRefundRequest
{
    public string? ProviderTransactionId { get; set; }
    public int ExpectedRowVersion { get; set; }
}
