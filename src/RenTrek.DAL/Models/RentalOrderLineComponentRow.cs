namespace RenTrek.DAL.Models;

/// <summary>
/// Thành phần chụp lại (snapshot) của một dòng combo trong đơn thuê.
/// </summary>
public class RentalOrderLineComponentRow
{
    public Guid VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int QuantityPerBundle { get; set; }
}
