namespace RenTrek.DAL.Models;

/// <summary>
/// Một tài khoản trong danh sách / chi tiết trang quản trị.
/// </summary>
public class AdminUserRow
{
    public Guid UserId { get; set; }
    public string? UserCode { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
