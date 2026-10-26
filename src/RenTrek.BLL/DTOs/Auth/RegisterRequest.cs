namespace RenTrek.BLL.DTOs.Auth;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ register.
/// </summary>
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
