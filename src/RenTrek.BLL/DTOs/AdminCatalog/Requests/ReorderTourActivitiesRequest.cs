namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body sắp lại thứ tự hoạt động trong một ngày (§6.15). Danh sách phải đủ mọi hoạt động của ngày đó.</summary>
public sealed class ReorderTourActivitiesRequest
{
    public int DayNumber { get; set; }
    public List<Guid> ActivityIdsInOrder { get; set; } = [];
}
