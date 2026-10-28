namespace RenTrek.BLL.DTOs.Rental;

/// <summary>
/// Kết quả khả dụng của một dòng thuê.
/// </summary>
public class RentalLineAvailability
{
    /// <summary>Chỉ số dòng trong danh sách đầu vào (0-based).</summary>
    public int LineIndex { get; set; }

    /// <summary>Dòng đáp ứng được khi xét tổng nhu cầu cộng dồn của cả giỏ.</summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Số lượng tối đa của riêng mục này còn thuê được (lẻ: số tài sản khả dụng; combo: min ⌊khả dụng / SL thành phần⌋).
    /// Không trừ nhu cầu các dòng khác.
    /// </summary>
    public int AvailableQuantity { get; set; }

    public List<RentalAvailabilityShortage> Shortages { get; set; } = [];

    /// <summary>Thành phần giới hạn số lượng (combo); null với dòng lẻ.</summary>
    public RentalAvailabilityShortage? LimitingComponent { get; set; }
}
