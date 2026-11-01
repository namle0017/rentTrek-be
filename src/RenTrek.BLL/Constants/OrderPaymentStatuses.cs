namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị orders.payment_status (cột varchar, không dùng enum).
/// </summary>
public static class OrderPaymentStatuses
{
    public const string Unpaid = "Unpaid";
    public const string Paid = "Paid";
    public const string PartiallyRefunded = "PartiallyRefunded";
    public const string Refunded = "Refunded";
    public const string NeedsReconciliation = "NeedsReconciliation";
}
