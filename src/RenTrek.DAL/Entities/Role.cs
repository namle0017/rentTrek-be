namespace RenTrek.DAL.Entities;

public class Role
{
    public long RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";

    public ICollection<User> Users { get; set; } = new List<User>();
}
