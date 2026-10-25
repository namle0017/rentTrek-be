namespace RenTrek.DAL.Entities;

public class SystemConfiguration
{
    public Guid ConfigurationId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public string ConfigKey { get; set; } = null!;
    public string ConfigValue { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
    public string ValueType { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Account UpdatedByUser { get; set; } = null!;
}
