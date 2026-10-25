namespace RenTrek.BLL.DTOs.Users;

public class UserSessionResponse
{
    public string FamilyId { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
}
