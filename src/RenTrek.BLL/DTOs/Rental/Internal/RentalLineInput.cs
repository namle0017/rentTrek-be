namespace RenTrek.BLL.DTOs.Rental.Internal;

/// <summary>
/// Một dòng giỏ thuê: đúng một trong ProductVariantId hoặc BundleId.
/// </summary>
public class RentalLineInput
{
    public Guid? ProductVariantId { get; set; }
    public Guid? BundleId { get; set; }
    public int Quantity { get; set; }
}
