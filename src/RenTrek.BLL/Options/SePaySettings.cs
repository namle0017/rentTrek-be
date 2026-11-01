namespace RenTrek.BLL.Options;

/// <summary>
/// Chứa thiết lập SePay: API key xác thực webhook và tài khoản ngân hàng nhận chuyển khoản (VietQR).
/// </summary>
public class SePaySettings
{
    public const string SectionName = "SePay";

    public ApiSettings Api { get; set; } = new();
    public string AuthMode { get; set; } = "ApiKey";
    public string WebhookSecret { get; set; } = string.Empty;
    public string? SubAccount { get; set; }

    /// <summary>Mã ngân hàng theo qr.sepay.vn, ví dụ MBBank, Vietcombank.</summary>
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    public class ApiSettings
    {
        /// <summary>API key cấu hình ở SePay; webhook gửi header <c>Authorization: Apikey {Key}</c>.</summary>
        public string Key { get; set; } = string.Empty;
    }
}
