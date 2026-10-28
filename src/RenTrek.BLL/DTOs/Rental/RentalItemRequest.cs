namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Một mục thuê trong request: đúng một trong ProductVariantId (thuê lẻ) hoặc BundleId (thuê combo).
/// </summary>
public class RentalItemRequest
{
    public Guid? ProductVariantId { get; set; }
    public Guid? BundleId { get; set; }
    public int Quantity { get; set; }
}
