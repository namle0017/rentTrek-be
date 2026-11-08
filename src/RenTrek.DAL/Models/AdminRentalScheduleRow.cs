namespace RenTrek.DAL.Models;

/// <summary>Dòng thiết bị cần giao hoặc cần nhận lại theo ngày trong lịch vận hành đơn thuê.</summary>
public class AdminRentalScheduleRow
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
