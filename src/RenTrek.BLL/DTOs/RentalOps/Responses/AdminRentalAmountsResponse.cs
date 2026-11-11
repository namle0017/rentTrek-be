namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalAmountsResponse
{
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundedAmount { get; set; }
}
