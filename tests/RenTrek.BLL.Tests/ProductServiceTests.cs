using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.Product.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Mappings;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
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
            InStockOnly = true,
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
        Assert.True(capturedFilter.InStockOnly);
        Assert.Equal("pricePerDay", capturedFilter.SortBy);
        Assert.False(capturedFilter.IsDescending);

        var item = Assert.Single(response.Items);
        Assert.Equal(productId, item.Id);
        Assert.Equal(95000m, item.PricePerDay);
        Assert.True(item.IsRentable);
    }

    [Fact]
    public async Task GetRelatedAsync_ReturnsSameCategoryProductsAndClampsLimit()
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var relatedId = Guid.NewGuid();
        var capturedLimit = 0;

        var currentProduct = new Product
        {
            Id = productId,
            CategoryId = categoryId,
            Name = "Lều 2 người",
            Category = new Category { Id = categoryId, Name = "Lều trại" },
            Status = CatalogStatus.Active
        };

        var relatedProducts = new List<Product>
        {
            new()
            {
                Id = relatedId,
                CategoryId = categoryId,
                Name = "Lều 4 người",
                Category = new Category { Id = categoryId, Name = "Lều trại" },
                Status = CatalogStatus.Active,
                Variants =
                [
                    new ProductVariant
                    {
                        Id = Guid.NewGuid(),
                        Status = CatalogStatus.Active,
                        RentalPriceTiers = [new RentalPriceTier { MinDays = 1, MaxDays = 3, DailyPrice = 140000m }]
                    }
                ]
            }
        };

        var productRepo = TestProxy.Create<IProductRepository>(
            ("GetPublicDetailAsync", args =>
            {
                var id = (Guid)args[0]!;
                return Task.FromResult(id == productId ? currentProduct : null);
            }),
            ("GetRelatedPublicAsync", args =>
            {
                capturedLimit = (int)args[2]!;
                return Task.FromResult(relatedProducts);
            }));

        var unitOfWork = TestProxy.Create<IUnitOfWork>(("get_Products", _ => productRepo));
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<ProductProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        var service = new ProductService(unitOfWork, mapper);

        var result = await service.GetRelatedAsync(productId, limit: 50);

        Assert.Equal(12, capturedLimit);
        var item = Assert.Single(result);
        Assert.Equal(relatedId, item.Id);
        Assert.Equal(140000m, item.PricePerDay);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetRelatedAsync(Guid.NewGuid(), 4));
    }

    [Fact]
    public async Task UpdateProductStatusAsync_TogglesStatusAndRejectsInvalidStatus()
    {
        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Bếp gas dã ngoại",
            Status = CatalogStatus.Active
        };

        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_AdminOperations", _ => TestProxy.Create<IAdminOperationRepository>(("AcquireLockAsync", _ => Task.CompletedTask))),
            ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", _ => Task.CompletedTask))),
            ("get_Products", _ => TestProxy.Create<IProductRepository>(
                ("GetByIdAsync", _ => Task.FromResult<Product?>(product)))));

        var currentUser = TestProxy.Create<ICurrentUserService>(
            ("get_Role", _ => RoleNames.Admin),
            ("get_RequiredUserId", _ => Guid.NewGuid()),
            ("get_UserId", _ => (Guid?)Guid.NewGuid()));

        var service = new AdminProductCatalogService(uow, currentUser, TimeProvider.System);

        await service.UpdateProductStatusAsync(productId, new UpdateProductStatusRequest { Status = "Inactive" }, CancellationToken.None);
        Assert.Equal(CatalogStatus.Inactive, product.Status);

        await service.UpdateProductStatusAsync(productId, new UpdateProductStatusRequest { Status = "Active" }, CancellationToken.None);
        Assert.Equal(CatalogStatus.Active, product.Status);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProductStatusAsync(productId, new UpdateProductStatusRequest { Status = "Archived" }, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteProductAsync_CascadesSoftDeleteToChildVariants()
    {
        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Đèn pin đội đầu",
            Status = CatalogStatus.Active
        };
        var variants = new List<ProductVariant>
        {
            new() { Id = Guid.NewGuid(), ProductId = productId, Sku = "LAMP-01", Name = "Bản tiêu chuẩn", Status = CatalogStatus.Active },
            new() { Id = Guid.NewGuid(), ProductId = productId, Sku = "LAMP-02", Name = "Bản pin lớn", Status = CatalogStatus.Active }
        };

        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_AdminOperations", _ => TestProxy.Create<IAdminOperationRepository>(("AcquireLockAsync", _ => Task.CompletedTask))),
            ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", _ => Task.CompletedTask))),
            ("get_Products", _ => TestProxy.Create<IProductRepository>(
                ("GetByIdAsync", _ => Task.FromResult<Product?>(product)))),
            ("get_ProductVariants", _ => TestProxy.Create<IProductVariantRepository>(
                ("GetByProductForUpdateAsync", _ => Task.FromResult(variants)))));

        var currentUser = TestProxy.Create<ICurrentUserService>(
            ("get_Role", _ => RoleNames.Admin),
            ("get_RequiredUserId", _ => Guid.NewGuid()),
            ("get_UserId", _ => (Guid?)Guid.NewGuid()));

        var service = new AdminProductCatalogService(uow, currentUser, TimeProvider.System);

        await service.DeleteProductAsync(productId, CancellationToken.None);

        Assert.True(product.IsDeleted);
        Assert.Equal(CatalogStatus.Inactive, product.Status);
        Assert.All(variants, variant =>
        {
            Assert.True(variant.IsDeleted);
            Assert.Equal(CatalogStatus.Inactive, variant.Status);
        });
    }

    [Fact]
    public async Task SetPrimaryVariantImageAsync_PromotesTargetImageAndUnsetsPreviousPrimary()
    {
        var variantId = Guid.NewGuid();
        var firstImageId = Guid.NewGuid();
        var secondImageId = Guid.NewGuid();

        var variant = new ProductVariant
        {
            Id = variantId,
            Sku = "TENT-2P",
            Name = "Lều 2 người",
            Status = CatalogStatus.Active
        };

        var images = new List<ProductVariantImage>
        {
            new() { Id = firstImageId, ProductVariantId = variantId, ImageUrl = "https://img/1.jpg", IsPrimary = true, DisplayOrder = 0 },
            new() { Id = secondImageId, ProductVariantId = variantId, ImageUrl = "https://img/2.jpg", IsPrimary = false, DisplayOrder = 1 }
        };

        var uow = TestProxy.Create<IUnitOfWork>(
            ("BeginTransactionAsync", _ => Task.CompletedTask),
            ("CommitAsync", _ => Task.CompletedTask),
            ("RollbackAsync", _ => Task.CompletedTask),
            ("SaveChangesAsync", _ => Task.FromResult(1)),
            ("get_AdminOperations", _ => TestProxy.Create<IAdminOperationRepository>(("AcquireLockAsync", _ => Task.CompletedTask))),
            ("get_AuditLogs", _ => TestProxy.Create<IAuditLogRepository>(("AddAsync", _ => Task.CompletedTask))),
            ("get_ProductVariants", _ => TestProxy.Create<IProductVariantRepository>(
                ("GetByIdAsync", _ => Task.FromResult<ProductVariant?>(variant)))),
            ("get_ProductVariantImages", _ => TestProxy.Create<IProductVariantImageRepository>(
                ("GetByVariantAsync", _ => Task.FromResult(images)))));

        var currentUser = TestProxy.Create<ICurrentUserService>(
            ("get_Role", _ => RoleNames.Admin),
            ("get_RequiredUserId", _ => Guid.NewGuid()),
            ("get_UserId", _ => (Guid?)Guid.NewGuid()));

        var service = new AdminProductCatalogService(uow, currentUser, TimeProvider.System);

        await service.SetPrimaryVariantImageAsync(variantId, secondImageId, CancellationToken.None);

        Assert.False(images[0].IsPrimary);
        Assert.True(images[1].IsPrimary);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SetPrimaryVariantImageAsync(variantId, Guid.NewGuid(), CancellationToken.None));
    }
}
