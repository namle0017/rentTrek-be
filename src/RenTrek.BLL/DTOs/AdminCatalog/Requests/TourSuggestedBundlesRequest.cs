namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thay toàn bộ combo gợi ý của tour.</summary>
public sealed class TourSuggestedBundlesRequest
{
    public List<TourSuggestedBundleItemRequest> Items { get; set; } = [];
}
