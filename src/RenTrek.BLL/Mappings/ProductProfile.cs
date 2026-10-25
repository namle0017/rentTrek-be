using AutoMapper;
using RenTrek.BLL.DTOs.Product;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;

namespace RenTrek.BLL.Mappings;

/// <summary>
/// Khai báo ánh xạ AutoMapper cho product.
/// </summary>
public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductResponse>()
            .ForMember(response => response.CategoryName, options => options.MapFrom(product => product.Category.Name))
            .ForMember(response => response.PricePerDay, options => options.MapFrom(product => product.Variants
                .Where(variant => variant.Status == CatalogStatus.Active)
                .SelectMany(variant => variant.RentalPriceTiers)
                .Select(tier => (decimal?)tier.DailyPrice).Min()))
            .ForMember(response => response.ThumbnailUrl, options => options.MapFrom(product => product.Variants
                .Where(variant => variant.Status == CatalogStatus.Active
                    && variant.RentalPriceTiers.Any())
                .SelectMany(variant => variant.Images)
                .OrderByDescending(image => image.IsPrimary).ThenBy(image => image.DisplayOrder).ThenBy(image => image.Id)
                .Select(image => image.ImageUrl).FirstOrDefault()));

        CreateMap<Product, ProductDetailResponse>()
            .IncludeBase<Product, ProductResponse>()
            .ForMember(response => response.Variants, options => options.MapFrom(product => product.Variants
                .Where(variant => variant.Status == CatalogStatus.Active
                    && variant.RentalPriceTiers.Any())
                .OrderBy(variant => variant.Id)));

        CreateMap<ProductVariant, ProductVariantResponse>()
            .ForMember(response => response.Images, options => options.MapFrom(variant => variant.Images
                .OrderByDescending(image => image.IsPrimary).ThenBy(image => image.DisplayOrder).ThenBy(image => image.Id)))
            .ForMember(response => response.RentalPriceTiers, options => options.MapFrom(variant => variant.RentalPriceTiers
                .OrderBy(tier => tier.MinDays).ThenBy(tier => tier.Id)));
        CreateMap<ProductVariantImage, ProductImageResponse>();
        CreateMap<RentalPriceTier, RentalPriceTierResponse>();
        CreateMap<Category, ProductCategoryResponse>();
    }
}
