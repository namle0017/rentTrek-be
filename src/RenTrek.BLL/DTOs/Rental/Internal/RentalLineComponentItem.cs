namespace RenTrek.BLL.DTOs.Rental.Internal;

/// <summary>
/// Biến thể thiết bị mà một dòng thuê cần. Dòng lẻ có 1 component (QuantityPerUnit = 1);
/// dòng combo có mỗi product_bundle_items một component (QuantityPerUnit = số lượng trong combo).
/// </summary>
public class RentalLineComponentItem
{
    public Guid ProductVariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public int QuantityPerUnit { get; set; }

    /// <summary>QuantityPerUnit nhân số lượng của dòng.</summary>
    public int TotalQuantity { get; set; }
}
