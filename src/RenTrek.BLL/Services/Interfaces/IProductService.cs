using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Product.Requests;
using RenTrek.BLL.DTOs.Product.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IProductService
{
    Task<PagedResponse<ProductResponse>> GetAllAsync(ProductQueryRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}
