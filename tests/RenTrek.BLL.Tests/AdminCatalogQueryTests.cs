using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Truy vấn danh mục cho vận hành (tour / sản phẩm) chạy trên Postgres thật qua repository.
/// </summary>
public sealed class AdminCatalogQueryTests
{
    [AdminPostgresFact]
    public async Task Tours_ReturnsOnlyLatestVersionAndProjectsOperationalFields()
    {
        await using var db = new AdminTestDatabase();
        await db.InitializeAsync();
        var now = db.Now;
        var familyId = Guid.NewGuid();
        Guid latestId;
        await using (var context = db.Context())
        {
            var category = new Category { Name = "Săn mây", CategoryType = "TOUR" };
            var location = new Location { Name = "Bắc Yên", Address = "Sơn La" };
            var oldVersion = NewTour(familyId, 1, "Tà Xùa cũ", category, location, db.AdminId);
            var latest = NewTour(familyId, 2, "Tà Xùa săn mây", category, location, db.AdminId);
            latest.Status = "Active";
            latest.ItineraryLockedAt = now.UtcDateTime;
            latest.Images.Add(new TourImage { ImageUrl = "secondary.jpg", DisplayOrder = 0, IsPrimary = false });
            latest.Images.Add(new TourImage { ImageUrl = "primary.jpg", DisplayOrder = 1, IsPrimary = true });
            latest.Trips.Add(NewTrip(now.AddDays(2), false, db));
            latest.Trips.Add(NewTrip(now.AddDays(-2), false, db));
            latest.Trips.Add(NewTrip(now.AddDays(3), true, db));
            context.Tours.AddRange(oldVersion, latest);
            await context.SaveChangesAsync();
            latestId = latest.Id;
        }

        await using var read = db.Context();
        var (items, total) = await new UnitOfWork(read).Tours.GetAdminPagedAsync(new AdminTourFilter
        {
            Search = "Tà Xùa",
            Statuses = ["Published", "Active"],
            Now = now
        });

        var tour = Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal(latestId, tour.TourId);
        Assert.Equal("Active", tour.Status);
        Assert.True(tour.IsItineraryLocked);
        Assert.Equal("primary.jpg", tour.PrimaryImageUrl);
        Assert.Equal(1, tour.UpcomingTripsCount);
    }

    /// <summary>
    /// Khách chỉ thấy 1 thẻ cho mỗi họ tour (bản đang bán mới nhất, bỏ qua bản Draft mới hơn),
    /// và chuyến Open của bản cũ còn đang bán được gộp vào thẻ/chi tiết. Bản cũ bị Archived thì thôi gộp.
    /// </summary>
    [AdminPostgresFact]
    public async Task CustomerTours_ShowOneCardPerFamilyAndKeepOldVersionTrips()
    {
        await using var db = new AdminTestDatabase();
        await db.InitializeAsync();
        var future = DateTimeOffset.UtcNow.AddDays(30); // repo so với giờ thật, không phải db.Now
        var familyId = Guid.NewGuid();
        Tour v1, v2;
        await using (var context = db.Context())
        {
            var category = new Category { Name = "Săn mây", CategoryType = "TOUR" };
            var location = new Location { Name = "Bắc Yên", Address = "Sơn La" };
            v1 = NewTour(familyId, 1, "Tà Xùa v1", category, location, db.AdminId);
            v2 = NewTour(familyId, 2, "Tà Xùa v2", category, location, db.AdminId);
            var v3 = NewTour(familyId, 3, "Tà Xùa v3 nháp", category, location, db.AdminId);
            v1.Status = "Active";
            v2.Status = "Published";
            v1.Trips.Add(NewTrip(future, false, db));
            v2.Trips.Add(NewTrip(future.AddDays(10), false, db));
            context.Tours.AddRange(v1, v2, v3);
            await context.SaveChangesAsync();
        }

        await using (var read = db.Context())
        {
            var tours = new UnitOfWork(read).Tours;
            var (items, total) = await tours.GetPagedToursAsync(new TourFilterParams(Search: "Tà Xùa"));
            var card = Assert.Single(items);
            Assert.Equal(1, total);
            Assert.Equal(v2.Id, card.Id);
            Assert.Equal([v1.Id, v2.Id], card.Trips.OrderBy(t => t.StartAt).Select(t => t.TourId));

            var detail = await tours.GetTourDetailByIdAsync(v2.Id);
            Assert.Equal(2, detail!.Trips.Count);
        }

        await using (var write = db.Context())
        {
            (await write.Tours.FindAsync(v1.Id))!.Status = "Archived";
            await write.SaveChangesAsync();
        }

        await using (var read = db.Context())
        {
            var tours = new UnitOfWork(read).Tours;
            Assert.Equal(v2.Id, Assert.Single((await tours.GetTourDetailByIdAsync(v2.Id))!.Trips).TourId);
            // Bản Archived vẫn mở được để xem lại, chỉ có chuyến của chính nó
            Assert.Equal(v1.Id, Assert.Single((await tours.GetTourDetailByIdAsync(v1.Id))!.Trips).TourId);
        }
    }

    [AdminPostgresFact]
    public async Task Products_FiltersStatusAndCountsOnlyCurrentInventory()
    {
        await using var db = new AdminTestDatabase();
        await db.InitializeAsync();
        await using (var context = db.Context())
        {
            var category = new Category { Name = "Lều trại", CategoryType = "PRODUCT" };
            var product = new Product { Name = "Lều Naturehike", Category = category, Status = CatalogStatus.Active, CreatedByUserId = db.AdminId };
            var variant = new ProductVariant { Product = product, Name = "Xanh rêu", Sku = "LEU-NH-XR", Status = CatalogStatus.Active };
            variant.Images.Add(new ProductVariantImage { ImageUrl = "tent.jpg", IsPrimary = true });
            variant.RentalPriceTiers.Add(new RentalPriceTier { MinDays = 1, MaxDays = 3, DailyPrice = 100000 });
            variant.EquipmentAssets.Add(NewAsset("Available", false));
            variant.EquipmentAssets.Add(NewAsset("Maintenance", false));
            variant.EquipmentAssets.Add(NewAsset("Available", true));
            context.ProductVariants.AddRange(variant,
                new ProductVariant { Product = product, Name = "Đã xóa", Sku = "DELETED", IsDeleted = true });
            await context.SaveChangesAsync();
        }

        await using var read = db.Context();
        var products = new UnitOfWork(read).Products;
        var (items, total) = await products.GetAdminPagedAsync(new AdminProductFilter { Search = "Naturehike", Status = CatalogStatus.Active });

        var row = Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal(1, row.VariantsCount);
        Assert.Equal(2, row.TotalAssetsCount);
        Assert.Equal(1, row.AvailableAssetsCount);
        Assert.Equal("tent.jpg", row.PrimaryImageUrl);

        var detail = await products.GetAdminDetailAsync(row.ProductId);
        var variantDetail = Assert.Single(detail!.Variants);
        Assert.Equal(2, variantDetail.EquipmentAssets.Count);

        // Các truy vấn công khai dịch được sang SQL (sort theo giá dùng subquery)
        var (publicItems, _) = await products.GetPublicPagedAsync(new ProductFilter { SortBy = "priceperday", IsDescending = true });
        Assert.Contains(publicItems, p => p.Id == row.ProductId);

        var (rentalItems, _) = await products.GetPublicPagedAsync(new ProductFilter
        {
            OfferingType = "Rental",
            MinPrice = 80000m,
            MaxPrice = 120000m
        });
        Assert.Contains(rentalItems, p => p.Id == row.ProductId);

        var (saleItems, saleTotal) = await products.GetPublicPagedAsync(new ProductFilter { OfferingType = "Sale" });
        Assert.Empty(saleItems);
        Assert.Equal(0, saleTotal);

        var (outOfRangeItems, _) = await products.GetPublicPagedAsync(new ProductFilter { MinPrice = 150000m });
        Assert.DoesNotContain(outOfRangeItems, p => p.Id == row.ProductId);

        Assert.NotNull(await products.GetPublicDetailAsync(row.ProductId));
        Assert.Contains(await products.GetPublicCategoriesAsync(), c => c.Name == "Lều trại");
    }

    private static Tour NewTour(Guid familyId, int version, string name, Category category, Location location, Guid createdBy)
        => new()
        {
            TourFamilyId = familyId,
            Version = version,
            Name = name,
            Category = category,
            MainLocation = location,
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 2_450_000m,
            Status = "Draft",
            CreatedByUserId = createdBy
        };

    private static Trip NewTrip(DateTimeOffset startAt, bool isDeleted, AdminTestDatabase db)
        => new()
        {
            TripId = Guid.NewGuid(),
            RefundPolicyId = db.PolicyId,
            ManagedByUserId = db.StaffId,
            StartAt = startAt,
            EndAt = startAt.AddDays(2),
            TotalSlots = 10,
            MinParticipants = 3,
            Status = "Open",
            CreatedAt = db.Now,
            UpdatedAt = db.Now,
            IsDeleted = isDeleted
        };

    private static EquipmentAsset NewAsset(string status, bool isDeleted)
        => new()
        {
            EquipmentAssetId = Guid.NewGuid(),
            AssetCode = Guid.NewGuid().ToString("N"),
            AssetStatus = status,
            IsDeleted = isDeleted
        };
}
