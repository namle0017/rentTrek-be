namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Các khoản tiền của đơn thuê.
/// </summary>
public class RentalOrderAmountsResponse
{
    public decimal SubtotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>Luôn 0 (hệ thống không thu cọc).</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>Số tiền khách cần thanh toán, bằng TotalAmount.</summary>
    public decimal TotalPayable { get; set; }

    /// <summary>Tổng tiền đã nhận, gồm tiền chuyển một phần; không đồng nghĩa order đã Confirmed.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Tổng các giao dịch hoàn (Refund) đã thành công.</summary>
    public decimal RefundedAmount { get; set; }
}
