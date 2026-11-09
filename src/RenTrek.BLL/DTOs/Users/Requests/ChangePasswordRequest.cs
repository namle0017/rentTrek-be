namespace RenTrek.BLL.DTOs.Users.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ change password.
/// Yêu cầu xác thực OTP 6 số gửi về email trước khi đổi mật khẩu.
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}
