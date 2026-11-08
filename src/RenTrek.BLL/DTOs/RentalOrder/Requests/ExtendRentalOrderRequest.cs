namespace RenTrek.BLL.DTOs.RentalOrder.Requests;

/// <summary>
/// Dữ liệu áp dụng gia hạn đơn thuê (POST /rental-orders/{orderId}/extensions): thời điểm trả mới và số tiền thêm khách đã đồng ý.
/// </summary>
public sealed class ExtendRentalOrderRequest
{
    /// <summary>Thời điểm trả mới (UTC), phải sau thời điểm trả hiện tại của đơn.</summary>
    public DateTime NewEndAt { get; set; }

    /// <summary>Số tiền thêm khách đã thấy ở báo giá; phải khớp đúng số tiền hệ thống tính lại khi áp dụng.</summary>
    public decimal AcceptedExtraAmount { get; set; }
}
