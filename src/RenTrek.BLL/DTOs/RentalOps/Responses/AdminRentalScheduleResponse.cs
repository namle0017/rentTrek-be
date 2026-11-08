namespace RenTrek.BLL.DTOs.RentalOps.Responses;

/// <summary>Danh sách thiết bị cần giao hoặc nhận trả trong một ngày (theo múi giờ Việt Nam UTC+7).</summary>
public sealed class AdminRentalScheduleResponse
{
    public DateOnly Date { get; set; }
    public int TotalItems { get; set; }
    public List<AdminRentalScheduleItemResponse> Items { get; set; } = [];
}
