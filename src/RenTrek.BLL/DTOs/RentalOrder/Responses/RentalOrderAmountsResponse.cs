namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Các khoản tiền của đơn thuê.
/// </summary>
public class RentalOrderAmountsResponse
{
    public decimal SubtotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>Tiền cọc (thu cùng tiền thuê, hoàn sau khi trả đồ trừ phí phát sinh).</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>Số tiền khách cần thanh toán = TotalAmount + DepositAmount.</summary>
    public decimal TotalPayable { get; set; }

    /// <summary>Tổng các giao dịch thu (Collection) đã thành công.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Tổng các giao dịch hoàn (Refund) đã thành công.</summary>
    public decimal RefundedAmount { get; set; }
}
