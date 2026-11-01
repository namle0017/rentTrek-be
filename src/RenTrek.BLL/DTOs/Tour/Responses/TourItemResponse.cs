namespace RenTrek.BLL.DTOs.Tour.Responses;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ tour item.
/// </summary>
public class TourItemResponse
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? QuantityBasis { get; set; }
    public int? Quantity { get; set; }
}
