namespace RenTrek.DAL.Entities;

public class Role
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
