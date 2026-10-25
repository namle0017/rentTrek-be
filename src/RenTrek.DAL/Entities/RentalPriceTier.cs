namespace RenTrek.DAL.Entities;

/// <summary>
/// Biểu diễn thực thể rental price tier trong mô hình dữ liệu RenTrek.
/// </summary>
public class RentalPriceTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProductVariantId { get; set; }
    public Guid? BundleId { get; set; }
    public int MinDays { get; set; } = 1;
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public ProductBundle? Bundle { get; set; }
}
