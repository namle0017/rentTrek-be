namespace RenTrek.BLL.DTOs.Users;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ change password.
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
