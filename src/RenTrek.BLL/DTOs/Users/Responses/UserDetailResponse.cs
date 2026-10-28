namespace RenTrek.BLL.DTOs.Users.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ user detail.
/// </summary>
public class UserDetailResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
