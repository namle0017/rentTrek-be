namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Tiêu chí 5: Toàn bộ đơn đặt chỗ của khách đã thanh toán hợp lệ (8.5).
/// </summary>
public sealed class ReadinessOrdersPaidItemResponse
{
    public bool Passed { get; set; }
    public int UnpaidOrdersCount { get; set; }
}
