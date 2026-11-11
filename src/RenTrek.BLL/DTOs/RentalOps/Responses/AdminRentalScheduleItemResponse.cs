namespace RenTrek.BLL.DTOs.RentalOps.Responses;

/// <summary>Chi tiết một thiết bị trong danh sách giao đồ hoặc nhận trả đồ thuê theo ngày.</summary>
public sealed class AdminRentalScheduleItemResponse
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
