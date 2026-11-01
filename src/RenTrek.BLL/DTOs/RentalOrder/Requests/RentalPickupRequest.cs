namespace RenTrek.BLL.DTOs.RentalOrder.Requests;

/// <summary>
/// Thông tin nhận đồ của khách (chưa có cột riêng nên được gộp vào ghi chú đơn).
/// </summary>
public class RentalPickupRequest
{
    public string? ReceiverName { get; set; }

    public string? ReceiverPhone { get; set; }

    /// <summary>Hiện chỉ nhận <c>Store</c>.</summary>
    public string? Method { get; set; }
}
