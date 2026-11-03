namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Tài khoản khách nhận hoàn; không lưu vào hồ sơ Customer.
/// </summary>
public sealed class RefundBankAccountRequest
{
    public string BankCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
}
