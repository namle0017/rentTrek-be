namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Thành phần của combo trong một dòng báo giá.
/// </summary>
public class RentalQuoteComponentResponse
{
    public Guid ProductVariantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int QuantityPerBundle { get; set; }

    public int TotalQuantity { get; set; }
}
