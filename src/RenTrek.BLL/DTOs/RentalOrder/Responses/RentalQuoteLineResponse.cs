namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Một dòng thuê trong báo giá.
/// </summary>
public class RentalQuoteLineResponse
{
    public int Index { get; set; }

    /// <summary>Variant | Bundle.</summary>
    public string ItemType { get; set; } = string.Empty;

    public Guid? ProductVariantId { get; set; }

    public Guid? BundleId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal DailyPrice { get; set; }

    public RentalQuoteTierResponse AppliedTier { get; set; } = new();

    public decimal LineAmount { get; set; }

    public bool IsAvailable { get; set; }

    /// <summary>Thông báo thiếu hàng; null khi đủ hàng.</summary>
    public string? AvailabilityMessage { get; set; }

    /// <summary>Thành phần combo (rỗng với dòng lẻ).</summary>
    public List<RentalQuoteComponentResponse> Components { get; set; } = [];
}
