namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Tóm tắt hồ sơ trả kèm booking và sau mỗi thao tác của Staff/Admin.
/// </summary>
public sealed class RefundCaseSummaryResponse
{
    public Guid RefundCaseId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string MaskedAccount { get; set; } = string.Empty;
    public int RowVersion { get; set; }
}
