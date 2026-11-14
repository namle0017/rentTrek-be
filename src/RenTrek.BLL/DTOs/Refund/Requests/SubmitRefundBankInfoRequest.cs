namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>Khách nhập tài khoản nhận hoàn cho hồ sơ đang chờ STK (no-show, hoàn cọc sau khi trả đồ).</summary>
public sealed class SubmitRefundBankInfoRequest
{
    public RefundBankAccountRequest? BankAccount { get; set; }
    public bool ConsentAccepted { get; set; }
    public string? ConsentVersion { get; set; }
}
