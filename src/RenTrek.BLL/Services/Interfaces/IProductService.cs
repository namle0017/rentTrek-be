using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.DTOs.Product;

namespace RenTrek.BLL.Services.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> GetAllAsync(ProductQueryRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}
