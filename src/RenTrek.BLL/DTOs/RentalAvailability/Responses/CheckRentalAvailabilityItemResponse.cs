namespace RenTrek.BLL.DTOs.RentalAvailability.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ kiểm tra khả dụng cho từng dòng thuê.
/// </summary>
public class CheckRentalAvailabilityItemResponse
{
    /// <summary>Chỉ số dòng trong request (0-based).</summary>
    public int Index { get; set; }
    public bool IsAvailable { get; set; }
    public int AvailableQuantity { get; set; }

    /// <summary>Thông báo thiếu hàng; null khi dòng đủ hàng.</summary>
    public string? Message { get; set; }
}
