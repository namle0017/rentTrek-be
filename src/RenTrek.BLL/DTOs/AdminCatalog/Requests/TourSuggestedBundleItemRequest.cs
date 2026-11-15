namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Một combo gợi ý kèm thứ tự và ghi chú cho khách.</summary>
public sealed class TourSuggestedBundleItemRequest
{
    public Guid BundleId { get; set; }
    public int SortOrder { get; set; }
    public string? Note { get; set; }
}
