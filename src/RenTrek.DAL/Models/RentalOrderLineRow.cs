namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng thuê (biến thể hoặc combo) của đơn thuê.
/// </summary>
public class RentalOrderLineRow
{
    public Guid ItemId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? BundleId { get; set; }
    public string? BundleName { get; set; }
    public string? ProductName { get; set; }
    public string? VariantName { get; set; }
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal DailyPrice { get; set; }
    public int BillableDays { get; set; }
    public decimal LineAmount { get; set; }

    /// <summary>Thành phần chụp lại khi đặt đơn (rỗng với dòng biến thể).</summary>
    public List<RentalOrderLineComponentRow> Components { get; set; } = [];
}
