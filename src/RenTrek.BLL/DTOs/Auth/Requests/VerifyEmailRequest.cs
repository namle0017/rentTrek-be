namespace RenTrek.BLL.DTOs.Auth.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào khi xác thực email tài khoản.
/// Hỗ trợ cả 2 hình thức:
/// 1. Token HMAC (từ đường link trong email /verify-email#t=...)
/// 2. Email + OTP 6 số (từ giao diện Modal xác thực khi vừa đăng ký)
/// </summary>
public sealed class VerifyEmailRequest
{
    public string? Token { get; set; }
    public string? Email { get; set; }
    public string? Otp { get; set; }
}
