namespace RenTrek.DAL.Entities;

public sealed class Account
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? EmailConfirmedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsDeleted { get; set; }
}
