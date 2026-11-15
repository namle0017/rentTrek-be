using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Product.Requests;
using RenTrek.BLL.DTOs.Product.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IProductService
{
    Task<PagedResponse<AdminProductListItemResponse>> GetAdminProductsAsync(AdminProductQueryRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<ProductResponse>> GetAllAsync(ProductQueryRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductResponse>> GetRelatedAsync(Guid id, int limit = 4, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<AdminProductDetailResponse> GetAdminProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
