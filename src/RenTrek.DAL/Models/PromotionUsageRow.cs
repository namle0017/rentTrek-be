namespace RenTrek.DAL.Models;

/// <summary>
/// Một lượt áp dụng khuyến mãi vào đơn.
/// </summary>
public class PromotionUsageRow
{
    public Guid AppliedPromotionId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public int? GiftQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
}
