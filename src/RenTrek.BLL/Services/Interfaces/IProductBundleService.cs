using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.ProductBundle;

namespace RenTrek.BLL.Services.Interfaces;

public interface IProductBundleService
{
    Task<PagedResult<ProductBundleResponse>> GetAllAsync(ProductBundleQueryRequest request, CancellationToken cancellationToken = default);
    Task<ProductBundleDetailResponse> GetByIdAsync(Guid bundleId, RentalPeriodQueryRequest request, CancellationToken cancellationToken = default);
}
