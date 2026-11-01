using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Product.Requests;
using RenTrek.BLL.DTOs.Product.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;
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

    private IQueryable<Product> PublicProducts => _unitOfWork.Repository<Product>().Query()
        .Where(product => product.Status == CatalogStatus.Active && product.Category.Status == "Active"
            && product.Variants.Any(variant => variant.Status == CatalogStatus.Active
                && variant.RentalPriceTiers.Any()));

    public async Task<PagedResponse<ProductResponse>> GetAllAsync(ProductQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = PublicProducts;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(product => product.Name.Contains(search)
                || (product.Description != null && product.Description.Contains(search)));
        }

        if (request.CategoryId.HasValue)
            query = query.Where(product => product.CategoryId == request.CategoryId.Value);

        var products = query.ProjectTo<ProductResponse>(_mapper.ConfigurationProvider);

        var sortedProducts = request.SortBy.ToLowerInvariant() switch
        {
            "name" => request.IsDescending ? products.OrderByDescending(product => product.Name) : products.OrderBy(product => product.Name),
            "priceperday" => request.IsDescending
                ? products.OrderBy(product => product.PricePerDay == null).ThenByDescending(product => product.PricePerDay)
                : products.OrderBy(product => product.PricePerDay == null).ThenBy(product => product.PricePerDay),
            _ => request.IsDescending ? products.OrderByDescending(product => product.CreatedAt) : products.OrderBy(product => product.CreatedAt)
        };

        return await sortedProducts.ThenBy(product => product.Id).ToPagedResponseAsync(request, cancellationToken);
    }

    public async Task<ProductDetailResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await PublicProducts.Where(product => product.Id == id)
            .ProjectTo<ProductDetailResponse>(_mapper.ConfigurationProvider)
            .AsSplitQuery().SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy sản phẩm");

    public async Task<IReadOnlyList<ProductCategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categoryIds = PublicProducts.Select(product => product.CategoryId);
        return await _unitOfWork.Repository<Category>().Query()
            .Where(category => categoryIds.Contains(category.Id))
            .OrderBy(category => category.Name).ThenBy(category => category.Id)
            .ProjectTo<ProductCategoryResponse>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
