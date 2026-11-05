namespace RenTrek.DAL.Models;

/// <summary>
/// Tổng tiền thanh toán thành công trong một khoảng thời gian.
/// </summary>
public class PaymentTotalsRow
{
    /// <summary>Tổng PaymentType = "Collection".</summary>
    public decimal Collections { get; set; }

    /// <summary>Tổng PaymentType = "Refund".</summary>
    public decimal Refunds { get; set; }

    /// <summary>Tổng Collection có Purpose = "AdditionalCharge".</summary>
    public decimal AdditionalCharges { get; set; }
}
