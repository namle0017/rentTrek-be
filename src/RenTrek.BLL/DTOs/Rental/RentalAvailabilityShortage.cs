namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Biến thể bị thiếu. Requested là TỔNG nhu cầu của biến thể trên mọi dòng giỏ (cộng dồn lẻ và combo).
/// </summary>
public class RentalAvailabilityShortage
{
    public Guid ProductVariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public int Requested { get; set; }
    public int Available { get; set; }
}
