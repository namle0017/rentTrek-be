using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Entities.Enums;

namespace RenTrek.BLL.Helpers;

public static class AdminCatalogQuery
{
    public static IQueryable<AdminTourListItemResponse> BuildTours(
        IQueryable<Tour> source,
        AdminTourQueryRequest request,
        DateTimeOffset now)
    {
        var tours = source.Where(tour => !tour.IsDeleted);
        var latestVersions = tours
            .GroupBy(tour => tour.TourFamilyId)
            .Select(group => new { TourFamilyId = group.Key, Version = group.Max(tour => tour.Version) });

        var query = tours.Where(tour => latestVersions.Any(latest =>
            latest.TourFamilyId == tour.TourFamilyId && latest.Version == tour.Version));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(tour => tour.Name.ToLower().Contains(search));
        }

        if (request.CategoryId.HasValue)
            query = query.Where(tour => tour.CategoryId == request.CategoryId.Value);

        if (request.LocationId.HasValue)
            query = query.Where(tour => tour.MainLocationId == request.LocationId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = request.Status.Trim().ToLowerInvariant() switch
            {
                "published" => query.Where(tour => tour.Status == TourStatuses.Published || tour.Status == TourStatuses.Active),
                "archived" => query.Where(tour => tour.Status == TourStatuses.Archived || tour.Status == TourStatuses.Expired),
                "draft" => query.Where(tour => tour.Status == TourStatuses.Draft),
                _ => query.Where(_ => false)
            };
        }

        return query
            .OrderByDescending(tour => tour.UpdatedAt)
            .ThenBy(tour => tour.Id)
            .Select(tour => new AdminTourListItemResponse
            {
                TourId = tour.Id,
                TourFamilyId = tour.TourFamilyId,
                Name = tour.Name,
                CategoryName = tour.Category.Name,
                MainLocationName = tour.MainLocation.Name,
                DurationDays = tour.DurationDays,
                DurationNights = tour.DurationNights,
                BasePrice = tour.BasePrice,
                Version = tour.Version,
                IsItineraryLocked = tour.ItineraryLockedAt.HasValue,
                Status = tour.Status == TourStatuses.Active ? "Published" : tour.Status == TourStatuses.Expired ? "Archived" : tour.Status,
                PrimaryImageUrl = tour.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault(),
                UpcomingTripsCount = tour.Trips.Count(trip => !trip.IsDeleted && trip.StartAt > now)
            });
    }

    public static IQueryable<AdminProductListItemResponse> BuildProducts(
        IQueryable<Product> source,
        AdminProductQueryRequest request)
    {
        var query = source.Where(product => !product.IsDeleted && !product.Category.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(product => product.Name.ToLower().Contains(search)
                || product.Variants.Any(variant => !variant.IsDeleted && variant.Sku.ToLower().Contains(search)));
        }

        if (request.CategoryId.HasValue)
            query = query.Where(product => product.CategoryId == request.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<CatalogStatus>(request.Status, ignoreCase: true, out var status))
                query = query.Where(product => product.Status == status);
            else
                query = query.Where(_ => false);
        }

        return query
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Id)
            .Select(product => new AdminProductListItemResponse
            {
                ProductId = product.Id,
                Name = product.Name,
                CategoryName = product.Category.Name,
                PrimaryImageUrl = product.Variants
                    .Where(variant => !variant.IsDeleted)
                    .SelectMany(variant => variant.Images.Where(image => !image.IsDeleted))
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault(),
                VariantsCount = product.Variants.Count(variant => !variant.IsDeleted),
                TotalAssetsCount = product.Variants
                    .Where(variant => !variant.IsDeleted)
                    .SelectMany(variant => variant.EquipmentAssets)
                    .Count(asset => !asset.IsDeleted),
                AvailableAssetsCount = product.Variants
                    .Where(variant => !variant.IsDeleted)
                    .SelectMany(variant => variant.EquipmentAssets)
                    .Count(asset => !asset.IsDeleted && asset.AssetStatus == EquipmentAssetStatuses.Available),
                Status = product.Status == CatalogStatus.Active ? "Active" : "Inactive",
                CreatedAt = product.CreatedAt
            });
    }
}
