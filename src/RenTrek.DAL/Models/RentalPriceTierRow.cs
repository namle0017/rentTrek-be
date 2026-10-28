namespace RenTrek.DAL.Models;

/// <summary>
/// Một bậc giá thuê theo ngày của biến thể hoặc combo (đúng một trong VariantId / BundleId có giá trị).
/// </summary>
public class RentalPriceTierRow
{
    public Guid Id { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? BundleId { get; set; }
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
    public decimal DailyPrice { get; set; }
}
