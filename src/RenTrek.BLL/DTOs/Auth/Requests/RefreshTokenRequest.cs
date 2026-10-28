namespace RenTrek.BLL.DTOs.Auth.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ refresh token.
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
