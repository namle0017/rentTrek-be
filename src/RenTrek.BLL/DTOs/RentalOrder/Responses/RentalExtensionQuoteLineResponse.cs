namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Chi tiết tính lại tiền của một dòng thuê khi gia hạn: số ngày, đơn giá theo bậc và thành tiền trước / sau.
/// </summary>
public sealed class RentalExtensionQuoteLineResponse
{
    public Guid RentalOrderItemId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int OldBillableDays { get; set; }

    public int NewBillableDays { get; set; }

    public decimal OldDailyPrice { get; set; }

    public decimal NewDailyPrice { get; set; }

    public decimal OldLineAmount { get; set; }

    public decimal NewLineAmount { get; set; }
}
