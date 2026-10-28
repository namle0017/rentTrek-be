namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Dòng thuê đã tính giá. LineAmount = Quantity × BillableDays × DailyPrice (VND).
/// </summary>
public class PricedRentalLine
{
    public int Index { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public Guid? ProductVariantId { get; set; }
    public Guid? BundleId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int BillableDays { get; set; }
    public Guid PriceTierId { get; set; }
    public int TierMinDays { get; set; }
    public int? TierMaxDays { get; set; }
    public decimal DailyPrice { get; set; }
    public decimal LineAmount { get; set; }
    public List<RentalLineComponent> Components { get; set; } = [];
}
