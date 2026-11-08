namespace RenTrek.BLL.DTOs.RentalOrder.Requests;

/// <summary>
/// Dữ liệu báo giá gia hạn đơn thuê (POST /rental-orders/{orderId}/extensions/quote): thời điểm trả mới (UTC).
/// </summary>
public sealed class ExtendRentalQuoteRequest
{
    /// <summary>Thời điểm trả mới, phải sau thời điểm trả hiện tại của đơn.</summary>
    public DateTime NewEndAt { get; set; }
}
