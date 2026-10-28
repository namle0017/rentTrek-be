using RenTrek.BLL.DTOs.Rental.Requests;

namespace RenTrek.BLL.DTOs.RentalOrder.Requests;

/// <summary>
/// Dữ liệu đầu vào báo giá đơn thuê (POST /rental-orders/quote). Mọi số tiền do server tính.
/// </summary>
public class RentalQuoteRequest
{
    public DateTime RentalStartAt { get; set; }

    public DateTime RentalEndAt { get; set; }

    public List<RentalItemRequest> Items { get; set; } = [];

    /// <summary>Mã khuyến mãi khách nhập (tối đa 50 ký tự).</summary>
    public string? PromotionCode { get; set; }
}
