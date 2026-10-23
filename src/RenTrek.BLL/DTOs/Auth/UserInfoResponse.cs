namespace RenTrek.BLL.DTOs.Auth;

public class UserInfoResponse
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
