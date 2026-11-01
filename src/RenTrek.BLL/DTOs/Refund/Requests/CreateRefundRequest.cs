namespace RenTrek.BLL.DTOs.Refund.Requests;

public sealed class CreateRefundRequest
{
    public string BankCode { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountHolderName { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string? Note { get; set; }
}
