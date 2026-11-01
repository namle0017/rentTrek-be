namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị orders.status (cột varchar, không dùng enum).
/// </summary>
public static class OrderStatuses
{
    public const string Draft = "Draft";
    public const string PendingPayment = "PendingPayment";
    public const string Confirmed = "Confirmed";
    public const string InFulfillment = "InFulfillment";
    public const string Completed = "Completed";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
}
