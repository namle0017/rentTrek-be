using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Helpers;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;

namespace RenTrek.BLL.Tests;

public sealed class AdminCatalogQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildTours_ReturnsOnlyLatestVersionAndProjectsOperationalFields()
    {
        var familyId = Guid.NewGuid();
        var category = new Category { Id = Guid.NewGuid(), Name = "Săn mây" };
        var location = new Location { Id = Guid.NewGuid(), Name = "Bắc Yên", Address = "Sơn La" };
        var oldVersion = CreateTour(familyId, 1, "Tà Xùa cũ", category, location);
        var latestVersion = CreateTour(familyId, 2, "Tà Xùa săn mây", category, location);
        latestVersion.Status = "Active";
        latestVersion.ItineraryLockedAt = Now.UtcDateTime;
        latestVersion.Images.Add(new TourImage
        {
            Id = Guid.NewGuid(),
            TourId = latestVersion.Id,
            Tour = latestVersion,
            ImageUrl = "secondary.jpg",
            DisplayOrder = 0,
            IsPrimary = false
        });
        latestVersion.Images.Add(new TourImage
        {
            Id = Guid.NewGuid(),
            TourId = latestVersion.Id,
            Tour = latestVersion,
            ImageUrl = "primary.jpg",
            DisplayOrder = 1,
            IsPrimary = true
        });
        latestVersion.Trips.Add(CreateTrip(latestVersion, Now.AddDays(2), isDeleted: false));
        latestVersion.Trips.Add(CreateTrip(latestVersion, Now.AddDays(-2), isDeleted: false));
        latestVersion.Trips.Add(CreateTrip(latestVersion, Now.AddDays(3), isDeleted: true));

        var result = AdminCatalogQuery.BuildTours(
                new[] { oldVersion, latestVersion }.AsQueryable(),
                new AdminTourQueryRequest(),
                Now)
            .ToList();

        var tour = Assert.Single(result);
        Assert.Equal(latestVersion.Id, tour.TourId);
        Assert.Equal("Tà Xùa săn mây", tour.Name);
        Assert.Equal("Published", tour.Status);
        Assert.True(tour.IsItineraryLocked);
        Assert.Equal("primary.jpg", tour.PrimaryImageUrl);
        Assert.Equal(1, tour.UpcomingTripsCount);
    }

    [Fact]
    public void BuildProducts_FiltersStatusAndCountsOnlyCurrentInventory()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Lều trại" };
        var active = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Name = "Lều Naturehike",
            Status = CatalogStatus.Active,
            CreatedAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = active.Id,
            Product = active,
            Name = "Xanh rêu",
            Sku = "LEU-NH-XR",
            Status = CatalogStatus.Active
        };
        variant.Images.Add(new ProductVariantImage
        {
            Id = Guid.NewGuid(),
            ProductVariantId = variant.Id,
            ProductVariant = variant,
            ImageUrl = "tent.jpg",
            IsPrimary = true
        });
        variant.EquipmentAssets.Add(CreateAsset(variant, "Available", isDeleted: false));
        variant.EquipmentAssets.Add(CreateAsset(variant, "Maintenance", isDeleted: false));
        variant.EquipmentAssets.Add(CreateAsset(variant, "Available", isDeleted: true));
        active.Variants.Add(variant);
        active.Variants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = active.Id,
            Product = active,
            Name = "Đã xóa",
            Sku = "DELETED",
            IsDeleted = true
        });

        var result = AdminCatalogQuery.BuildProducts(
                new[] { active }.AsQueryable(),
                new AdminProductQueryRequest { Status = "Active", Search = "Naturehike" })
            .ToList();

        var product = Assert.Single(result);
        Assert.Equal(1, product.VariantsCount);
        Assert.Equal(2, product.TotalAssetsCount);
        Assert.Equal(1, product.AvailableAssetsCount);
        Assert.Equal("tent.jpg", product.PrimaryImageUrl);
        Assert.Equal("Active", product.Status);
    }

    [Fact]
    public void AdminCatalogQueries_AreTranslatableByNpgsql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var context = new AppDbContext(options);

        var tourSql = AdminCatalogQuery.BuildTours(context.Tours, new AdminTourQueryRequest(), Now).ToQueryString();
        var productSql = AdminCatalogQuery.BuildProducts(context.Products, new AdminProductQueryRequest()).ToQueryString();

        Assert.False(string.IsNullOrWhiteSpace(tourSql));
        Assert.False(string.IsNullOrWhiteSpace(productSql));
    }

    private static Tour CreateTour(Guid familyId, int version, string name, Category category, Location location)
        => new()
        {
            Id = Guid.NewGuid(),
            TourFamilyId = familyId,
            Version = version,
            Name = name,
            CategoryId = category.Id,
            Category = category,
            MainLocationId = location.Id,
            MainLocation = location,
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 2_450_000m,
            Status = "Draft"
        };

    private static Trip CreateTrip(Tour tour, DateTimeOffset startAt, bool isDeleted)
        => new()
        {
            TripId = Guid.NewGuid(),
            TourId = tour.Id,
            Tour = tour,
            StartAt = startAt,
            EndAt = startAt.AddDays(2),
            IsDeleted = isDeleted
        };

    private static EquipmentAsset CreateAsset(ProductVariant variant, string status, bool isDeleted)
        => new()
        {
            EquipmentAssetId = Guid.NewGuid(),
            ProductVariantId = variant.Id,
            ProductVariant = variant,
            AssetCode = Guid.NewGuid().ToString("N"),
            AssetStatus = status,
            IsDeleted = isDeleted
        };
}
