using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.ProductBundle.Requests;
using RenTrek.BLL.DTOs.ProductBundle.Responses;
using RenTrek.BLL.DTOs.TripSuggestion.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IProductBundleService
{
    Task<PagedResponse<ProductBundleResponse>> GetAllAsync(ProductBundleQueryRequest request, CancellationToken cancellationToken = default);
    Task<ProductBundleDetailResponse> GetByIdAsync(Guid bundleId, RentalPeriodQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Combo gợi ý của tour cho chuyến, giá + còn hàng theo khoảng ngày của chuyến.</summary>
    Task<TripSuggestedBundlesResponse> GetSuggestedForTripAsync(Guid tripId, CancellationToken cancellationToken = default);
}
