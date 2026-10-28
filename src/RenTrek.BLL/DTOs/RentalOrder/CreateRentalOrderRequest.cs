namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Dữ liệu đầu vào tạo đơn thuê (POST /rental-orders): như báo giá, thêm ghi chú, thông tin nhận đồ và phiên bản chính sách đã đồng ý.
/// </summary>
public class CreateRentalOrderRequest : RentalQuoteRequest
{
    /// <summary>Ghi chú của khách, tối đa 1000 ký tự.</summary>
    public string? Note { get; set; }

    public RentalPickupRequest? Pickup { get; set; }

    /// <summary>Phiên bản chính sách hoàn tiền khách đã xem; phải khớp phiên bản hiện hành.</summary>
    public int AcceptedRefundPolicyVersion { get; set; }
}
