using AutoMapper;
using RenTrek.BLL.DTOs.Product;
using RenTrek.BLL.DTOs.ProductBundle;
using RenTrek.DAL.Models;

namespace RenTrek.BLL.Mappings;

/// <summary>
/// Khai báo ánh xạ AutoMapper cho product bundle.
/// </summary>
public class ProductBundleProfile : AutoMapper.Profile
{
    public ProductBundleProfile()
    {
        CreateMap<ProductBundleListRow, ProductBundleResponse>()
            .ForMember(response => response.Category, options => options.MapFrom(row => new ProductCategoryResponse
            {
                Id = row.CategoryId,
                Name = row.CategoryName
            }))
            .ForMember(response => response.ComponentSummary, options => options.MapFrom(row =>
                string.Join(" · ", row.Components.Select(component => component.ProductName + " ×" + component.Quantity))))
            .ForMember(response => response.Availability, options => options.Ignore());

        CreateMap<ProductBundleComponentRow, ProductBundleComponentResponse>()
            .ForMember(response => response.ProductVariantId, options => options.MapFrom(row => row.VariantId))
            .ForMember(response => response.AvailableQuantity, options => options.Ignore());

        CreateMap<RentalPriceTierRow, RentalPriceTierResponse>();

        CreateMap<ProductBundleDetailRow, ProductBundleDetailResponse>()
            .ForMember(response => response.Availability, options => options.Ignore());
    }
}
