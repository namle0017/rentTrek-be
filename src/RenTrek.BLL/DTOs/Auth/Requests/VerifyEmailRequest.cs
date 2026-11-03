namespace RenTrek.BLL.DTOs.Auth.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào khi xác thực email tài khoản.
/// </summary>
public sealed class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

