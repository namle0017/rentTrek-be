namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalLineResponse
{
    public Guid RentalOrderItemId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsBundle { get; set; }
    public int Quantity { get; set; }
    public decimal DailyPrice { get; set; }
    public int BillableDays { get; set; }
    public decimal LineAmount { get; set; }
}
