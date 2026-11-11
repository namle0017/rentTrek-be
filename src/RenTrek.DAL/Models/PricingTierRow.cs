namespace RenTrek.DAL.Models;

/// <summary>
/// Một bậc giá trong bảng giá tổng hợp (màn O18): của một biến thể (có Sku) hoặc một combo (Sku null).
/// </summary>
public class PricingTierRow
{
    public Guid Id { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? BundleId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
}
