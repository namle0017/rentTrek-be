namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể tour provided item trong mô hình dữ liệu RenTrek.
/// </summary>
public class TourProvidedItem : BaseEntity
{
    public Guid TourId { get; set; }
    public Tour Tour { get; set; } = null!;

    public Guid? ProductVariantId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string QuantityBasis { get; set; } = "PerPerson";
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public ICollection<TripProvidedItem> TripProvidedItems { get; set; } = [];
}
