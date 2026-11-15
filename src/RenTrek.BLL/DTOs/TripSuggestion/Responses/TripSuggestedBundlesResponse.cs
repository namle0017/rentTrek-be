using RenTrek.BLL.DTOs.ProductBundle.Responses;

namespace RenTrek.BLL.DTOs.TripSuggestion.Responses;

/// <summary>Combo gợi ý cho chuyến, giá và tình trạng còn hàng tính theo khoảng ngày của chuyến.</summary>
public sealed class TripSuggestedBundlesResponse
{
    public DateTime RentalFrom { get; set; }
    public DateTime RentalTo { get; set; }

    /// <summary>Biến thể tour đã cấp; FE đánh dấu để khách tránh thuê trùng.</summary>
    public List<Guid> ProvidedVariantIds { get; set; } = [];

    public List<TripSuggestedBundleResponse> Bundles { get; set; } = [];
}

