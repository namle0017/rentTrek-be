namespace RenTrek.BLL.Options;

/// <summary>
/// Chứa thiết lập gửi email qua SMTP (Gmail: smtp.gmail.com:587 + App Password) và địa chỉ FE dùng để tạo link trong email.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;

    /// <summary>Gmail yêu cầu App Password (bật 2FA), không dùng mật khẩu đăng nhập.</summary>
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "RenTrek";

    /// <summary>Gốc URL của FE, ví dụ http://localhost:5173.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
