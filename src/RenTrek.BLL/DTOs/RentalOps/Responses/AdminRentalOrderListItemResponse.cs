namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalOrderListItemResponse
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public decimal TotalAmount { get; set; }
    public int AssetsCount { get; set; }
    public int IssuedCount { get; set; }
    public int ReturnedCount { get; set; }
    public int DraftChargesCount { get; set; }
    public bool IsOverdue { get; set; }
}
