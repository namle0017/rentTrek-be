using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.BLL.DTOs.Product.Requests;
using RenTrek.BLL.Mappings;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Validators.Product;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class ProductServiceTests
{
    private readonly ProductQueryRequestValidator _validator = new();

    [Theory]
    [InlineData("Rental")]
    [InlineData("rental")]
    [InlineData("Sale")]
    [InlineData("Both")]
    [InlineData(null)]
    [InlineData("")]
    public void Validator_AcceptsSupportedOfferingTypes(string? offeringType)
    {
        var result = _validator.Validate(new ProductQueryRequest
        {
            OfferingType = offeringType,
            MinPrice = 50000m,
            MaxPrice = 200000m
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_RejectsInvalidOfferingTypeAndPriceRange()
    {
        var invalidOffering = _validator.Validate(new ProductQueryRequest { OfferingType = "Lease" });
        Assert.False(invalidOffering.IsValid);
        Assert.Contains(invalidOffering.Errors, error => error.PropertyName == nameof(ProductQueryRequest.OfferingType));

        var negativePrice = _validator.Validate(new ProductQueryRequest { MinPrice = -1m });
        Assert.False(negativePrice.IsValid);
        Assert.Contains(negativePrice.Errors, error => error.PropertyName == nameof(ProductQueryRequest.MinPrice));

        var invertedRange = _validator.Validate(new ProductQueryRequest { MinPrice = 200000m, MaxPrice = 100000m });
        Assert.False(invertedRange.IsValid);
        Assert.Contains(invertedRange.Errors, error => error.PropertyName == nameof(ProductQueryRequest.MaxPrice));
    }

    [Fact]
    public async Task GetAllAsync_ForwardsOfferingTypeAndPriceRangeToRepository()
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        ProductFilter? capturedFilter = null;

        IReadOnlyList<Product> seededProducts =
        [
            new Product
            {
                Id = productId,
                CategoryId = categoryId,
                Name = "Lều cắm trại 2 người",
                Category = new Category { Id = categoryId, Name = "Lều trại" },
                Status = CatalogStatus.Active,
                Variants =
                [
                    new ProductVariant
                    {
                        Id = Guid.NewGuid(),
                        Status = CatalogStatus.Active,
                        RentalPriceTiers = [new RentalPriceTier { MinDays = 1, MaxDays = 3, DailyPrice = 95000m }]
                    }
                ]
            }
        ];

        var productRepo = TestProxy.Create<IProductRepository>(
            ("GetPublicPagedAsync", args =>
            {
                capturedFilter = (ProductFilter)args[0]!;
                return Task.FromResult((seededProducts, 1));
            }));

        var unitOfWork = TestProxy.Create<IUnitOfWork>(
            ("get_Products", _ => productRepo));

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ProductProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        var service = new ProductService(unitOfWork, mapper);

        var response = await service.GetAllAsync(new ProductQueryRequest
        {
            Search = "Lều",
            CategoryId = categoryId,
            OfferingType = "Rental",
            MinPrice = 50000m,
            MaxPrice = 150000m,
            SortBy = "pricePerDay",
            SortOrder = "asc",
            PageNumber = 1,
            PageSize = 8
        });

        Assert.NotNull(capturedFilter);
        Assert.Equal("Lều", capturedFilter!.Search);
        Assert.Equal(categoryId, capturedFilter.CategoryId);
        Assert.Equal("Rental", capturedFilter.OfferingType);
        Assert.Equal(50000m, capturedFilter.MinPrice);
        Assert.Equal(150000m, capturedFilter.MaxPrice);
        Assert.Equal("pricePerDay", capturedFilter.SortBy);
        Assert.False(capturedFilter.IsDescending);

        var item = Assert.Single(response.Items);
        Assert.Equal(productId, item.Id);
        Assert.Equal(95000m, item.PricePerDay);
        Assert.True(item.IsRentable);
    }
}
