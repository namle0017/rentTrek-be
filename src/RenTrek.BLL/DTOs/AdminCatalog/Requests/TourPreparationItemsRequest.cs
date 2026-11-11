namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thay toàn bộ danh sách đồ tự mang (§6.17).</summary>
public sealed class TourPreparationItemsRequest
{
    public List<TourPreparationItemRequest> Items { get; set; } = [];
}
