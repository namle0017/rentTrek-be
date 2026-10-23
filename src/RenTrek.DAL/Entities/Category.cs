using RenTrek.DAL.Entities.Enums;

namespace RenTrek.DAL.Entities;

public class Category
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
