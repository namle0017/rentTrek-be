namespace RenTrek.BLL.DTOs.Auth;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ refresh token.
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
