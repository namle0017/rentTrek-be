namespace RenTrek.BLL.DTOs.Auth;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ user info.
/// </summary>
public class UserInfoResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
