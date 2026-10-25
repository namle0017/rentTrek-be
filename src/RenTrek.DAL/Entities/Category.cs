namespace RenTrek.DAL.Entities;

public class Category
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public string CategoryType { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<ProductBundle> ProductBundles { get; set; } = new List<ProductBundle>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Tour> Tours { get; set; } = new List<Tour>();
}
