namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Lý do mã khuyến mãi không áp dụng được.
/// </summary>
public class RentalQuotePromotionErrorResponse
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
