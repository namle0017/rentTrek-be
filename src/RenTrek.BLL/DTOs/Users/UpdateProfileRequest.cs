namespace RenTrek.BLL.DTOs.Users;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ update profile.
/// </summary>
public class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
