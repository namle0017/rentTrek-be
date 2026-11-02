namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Thông tin tọa độ và địa chỉ điểm tập trung của chuyến đi cho Guide (24.1).
/// </summary>
public sealed class GuideMeetingLocationResponse
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}
