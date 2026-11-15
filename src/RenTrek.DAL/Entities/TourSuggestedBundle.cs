namespace RenTrek.DAL.Entities;

/// <summary>Combo thuê gợi ý cho tour, theo thứ tự hiển thị.</summary>
public class TourSuggestedBundle
{
    public Guid TourSuggestedBundleId { get; set; }
    public Guid TourId { get; set; }
    public Guid BundleId { get; set; }
    public int SortOrder { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public Tour Tour { get; set; } = null!;
    public ProductBundle Bundle { get; set; } = null!;
}
