namespace RenTrek.DAL.Entities;

public class TourProvidedItem
{
    public Guid TourProvidedItemId { get; set; }
    public Guid TourId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string? Note { get; set; }
    public string QuantityBasis { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;
    public Tour Tour { get; set; } = null!;
    public ICollection<TripProvidedItem> TripProvidedItems { get; set; } = new List<TripProvidedItem>();
}
