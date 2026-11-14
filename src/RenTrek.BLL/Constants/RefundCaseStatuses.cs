namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị refund_cases.status (máy trạng thái ở docs/05-MANUAL-REFUND-WORKFLOW.md §4).
/// </summary>
public static class RefundCaseStatuses
{
    /// <summary>Đã có số tiền hoàn nhưng khách chưa nhập STK (no-show, hoàn cọc sau khi trả đồ).</summary>
    public const string AwaitingBankInfo = "AwaitingBankInfo";
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> AdminQueue = [AwaitingBankInfo, Requested, Approved, Completed, Rejected, Failed];
}
