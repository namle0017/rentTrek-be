using AutoMapper;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Product.Requests;
using RenTrek.BLL.DTOs.Product.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities.Enums;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ product.
/// </summary>
public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<AdminProductListItemResponse>> GetAdminProductsAsync(
        AdminProductQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        CatalogStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            // Trạng thái lạ → trang rỗng như trước
            if (!Enum.TryParse<CatalogStatus>(request.Status, ignoreCase: true, out var parsed))
                return Page(new List<AdminProductListItemResponse>(), request.PageNumber, request.PageSize, 0);
            status = parsed;
        }

        var (items, total) = await _unitOfWork.Products.GetAdminPagedAsync(new AdminProductFilter
        {
            Search = request.Search,
            CategoryId = request.CategoryId,
            Status = status,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);

        return Page(items.Select(product => new AdminProductListItemResponse
        {
            ProductId = product.ProductId,
            Name = product.Name,
            CategoryName = product.CategoryName,
            PrimaryImageUrl = product.PrimaryImageUrl,
            VariantsCount = product.VariantsCount,
            TotalAssetsCount = product.TotalAssetsCount,
            AvailableAssetsCount = product.AvailableAssetsCount,
            Status = ToDisplay(product.Status),
            CreatedAt = product.CreatedAt
        }).ToList(), request.PageNumber, request.PageSize, total);
    }

    public async Task<PagedResponse<ProductResponse>> GetAllAsync(ProductQueryRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _unitOfWork.Products.GetPublicPagedAsync(new ProductFilter
        {
            Search = request.Search,
            CategoryId = request.CategoryId,
            SortBy = request.SortBy,
            IsDescending = request.IsDescending,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);

        return Page(_mapper.Map<List<ProductResponse>>(items), request.PageNumber, request.PageSize, total);
    }

    public async Task<AdminProductDetailResponse> GetAdminProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetAdminDetailAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy sản phẩm");

        return new AdminProductDetailResponse
        {
            ProductId = product.Id,
            Name = product.Name,
            Description = product.Description,
            CategoryName = product.Category.Name,
            Status = ToDisplay(product.Status),
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Variants = product.Variants
                .OrderBy(variant => variant.Sku)
                .Select(variant => new AdminProductVariantResponse
                {
                    VariantId = variant.Id,
                    Sku = variant.Sku,
                    Name = variant.Name,
                    Size = variant.Size,
                    Color = variant.Color,
                    Status = ToDisplay(variant.Status),
                    ImageUrls = variant.Images
                        .OrderByDescending(image => image.IsPrimary)
                        .ThenBy(image => image.DisplayOrder)
                        .Select(image => image.ImageUrl)
                        .ToList(),
                    Images = variant.Images
                        .OrderByDescending(image => image.IsPrimary)
                        .ThenBy(image => image.DisplayOrder)
                        .Select(image => new AdminVariantImageResponse
                        {
                            ImageId = image.Id,
                            ImageUrl = image.ImageUrl,
                            DisplayOrder = image.DisplayOrder,
                            IsPrimary = image.IsPrimary
                        })
                        .ToList(),
                    PriceTiers = variant.RentalPriceTiers
                        .OrderBy(tier => tier.MinDays)
                        .Select(tier => new AdminPriceTierResponse(tier.MinDays, tier.MaxDays, tier.DailyPrice))
                        .ToList(),
                    TotalAssetsCount = variant.EquipmentAssets.Count,
                    AvailableAssetsCount = variant.EquipmentAssets
                        .Count(asset => asset.AssetStatus == EquipmentAssetStatuses.Available)
                })
                .ToList()
        };
    }

    public async Task<ProductDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _unitOfWork.Products.GetPublicDetailAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy sản phẩm");
        return _mapper.Map<ProductDetailResponse>(product);
    }

    public async Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => _mapper.Map<List<ProductCategoryResponse>>(await _unitOfWork.Products.GetPublicCategoriesAsync(cancellationToken));

    private static string ToDisplay(CatalogStatus status) => status == CatalogStatus.Active ? "Active" : "Inactive";

    private static PagedResponse<T> Page<T>(IReadOnlyList<T> items, int pageNumber, int pageSize, int total) => new()
    {
        Items = items,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalItems = total
    };
}
