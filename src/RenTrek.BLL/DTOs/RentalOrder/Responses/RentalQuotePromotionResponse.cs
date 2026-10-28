namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Khuyến mãi đã áp dụng trong báo giá.
/// </summary>
public class RentalQuotePromotionResponse
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal DiscountAmount { get; set; }

    public string? Description { get; set; }
}
