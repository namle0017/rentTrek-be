namespace RenTrek.BLL.DTOs.RentalAvailability;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ kiểm tra khả dụng nhiều dòng thuê.
/// </summary>
public class CheckRentalAvailabilityResponse
{
    public int BillableDays { get; set; }
    public bool IsAllAvailable { get; set; }
    public List<CheckRentalAvailabilityItemResponse> Items { get; set; } = [];
}
