using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Order.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ danh sách đơn hàng của tôi.
/// </summary>
public class MyOrderQueryRequest : PagingRequest
{
    /// <summary>Loại đơn: Rental | TripBooking; null = mọi loại.</summary>
    public string? OrderType { get; set; }

    /// <summary>Trạng thái đơn; null = mọi trạng thái.</summary>
    public string? Status { get; set; }
}
