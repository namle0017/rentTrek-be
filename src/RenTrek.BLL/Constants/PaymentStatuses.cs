namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị payments.status (cột varchar, không dùng enum).
/// </summary>
public static class PaymentStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}
