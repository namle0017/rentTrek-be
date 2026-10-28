namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Một dòng thuê (biến thể hoặc combo) của đơn thuê.
/// </summary>
public class RentalOrderLineResponse
{
    public Guid Id { get; set; }

    /// <summary>Variant | Bundle.</summary>
    public string ItemType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public int Quantity { get; set; }

    public decimal DailyPrice { get; set; }

    public int BillableDays { get; set; }

    public decimal LineAmount { get; set; }

    /// <summary>Thành phần combo đã chốt khi đặt đơn (rỗng với dòng lẻ).</summary>
    public List<RentalOrderBundleComponentResponse> BundleComponents { get; set; } = [];
}
