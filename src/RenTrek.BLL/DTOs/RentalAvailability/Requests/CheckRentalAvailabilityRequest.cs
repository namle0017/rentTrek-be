using RenTrek.BLL.DTOs.Rental.Requests;

namespace RenTrek.BLL.DTOs.RentalAvailability.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ kiểm tra khả dụng nhiều dòng thuê.
/// </summary>
public class CheckRentalAvailabilityRequest
{
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public List<RentalItemRequest> Items { get; set; } = [];
}
