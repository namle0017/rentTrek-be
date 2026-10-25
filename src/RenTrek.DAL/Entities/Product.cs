namespace RenTrek.DAL.Entities;

public class Product
{
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Category Category { get; set; } = null!;
    public Account CreatedByUser { get; set; } = null!;
    public ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}
