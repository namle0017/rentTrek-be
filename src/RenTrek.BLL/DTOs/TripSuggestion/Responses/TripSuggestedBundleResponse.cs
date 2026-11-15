using RenTrek.BLL.DTOs.ProductBundle.Responses;

namespace RenTrek.BLL.DTOs.TripSuggestion.Responses;

/// <summary>Một combo gợi ý kèm ghi chú của tour.</summary>
public sealed class TripSuggestedBundleResponse
{
    public string? Note { get; set; }
    public ProductBundleDetailResponse Bundle { get; set; } = null!;
}
