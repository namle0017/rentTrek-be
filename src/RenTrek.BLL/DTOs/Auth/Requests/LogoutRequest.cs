namespace RenTrek.BLL.DTOs.Auth.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ logout.
/// </summary>
public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
