namespace RenTrek.DAL.Entities;

public class TripProvidedItem
{
    public Guid TripProvidedItemId { get; set; }
    public Guid TripId { get; set; }
    public Guid TourProvidedItemId { get; set; }
    public Guid? PreparedByUserId { get; set; }
    public int RequiredQuantity { get; set; }
    public string Status { get; set; } = null!;
    public Guid ProductVariantId { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<AssetAssignment> AssetAssignments { get; set; } = new List<AssetAssignment>();
    public Account? PreparedByUser { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public TourProvidedItem TourProvidedItem { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
}
