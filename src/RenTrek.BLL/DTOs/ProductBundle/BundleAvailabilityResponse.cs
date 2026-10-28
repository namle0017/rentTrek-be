using System.Text.Json.Serialization;

namespace RenTrek.BLL.DTOs.ProductBundle;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ khả dụng combo trong khoảng from-to.
/// </summary>
public class BundleAvailabilityResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }

    /// <summary>Tên sản phẩm của thành phần giới hạn số lượng; chỉ có ở chi tiết combo.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LimitingComponent { get; set; }
}
