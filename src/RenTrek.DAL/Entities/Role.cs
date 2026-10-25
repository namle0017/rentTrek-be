namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể role trong mô hình dữ liệu RenTrek.
/// </summary>
public class Role
{
    public Guid RoleId { get; set; } = Guid.NewGuid();
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
