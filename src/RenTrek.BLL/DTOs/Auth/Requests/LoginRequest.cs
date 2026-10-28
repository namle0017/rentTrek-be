namespace RenTrek.BLL.DTOs.Auth.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ login.
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
