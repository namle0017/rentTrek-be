namespace RenTrek.BLL.DTOs.Auth;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ auth.
/// </summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public UserInfoResponse User { get; set; } = null!;
}
