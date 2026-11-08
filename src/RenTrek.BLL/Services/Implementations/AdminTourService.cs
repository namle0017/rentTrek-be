using System.Globalization;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Ghi tour cho vận hành (§6). Mọi thao tác ghi chạy trong transaction có khóa "tours" và ghi audit log.
/// Khóa lịch trình = đã đóng dấu ItineraryLockedAt hoặc đã có đơn đặt trên bất kỳ chuyến nào của tour.
/// </summary>
public sealed class AdminTourService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminTourService
{
    // ponytail: một khóa cho mọi tour, tách theo tourId nếu nhiều admin sửa đồng thời
    private const string LockKey = "tours";
    private const string EntityType = "Tour";

    public async Task<Guid> CreateTourAsync(SaveTourRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var tourId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            await EnsureCategoryAsync(request.CategoryId, cancellationToken);
            await EnsureLocationAsync(request.MainLocationId, cancellationToken);
            await UnitOfWork.Tours.AddAsync(new Tour
            {
                Id = tourId,
                TourFamilyId = Guid.NewGuid(),
                Version = 1,
                CreatedByUserId = ActorId,
                Name = request.Name.Trim(),
                CategoryId = request.CategoryId,
                MainLocationId = request.MainLocationId,
                Description = Trim(request.Description),
                DurationDays = request.DurationDays,
                DurationNights = request.DurationNights,
                DurationMinutes = request.DurationMinutes,
                BasePrice = request.BasePrice,
                Status = TourStatuses.Draft,
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await AuditAsync("Create", EntityType, tourId, request, cancellationToken);
        }, cancellationToken);
        return tourId;
    }

    public async Task<AdminTourDetailResponse> GetTourAsync(Guid tourId, CancellationToken cancellationToken)
    {
        var tour = await LoadAsync(tourId, cancellationToken);
        return new AdminTourDetailResponse
        {
            TourId = tour.Id,
            TourFamilyId = tour.TourFamilyId,
            Version = tour.Version,
            Name = tour.Name,
            CategoryId = tour.CategoryId,
            MainLocationId = tour.MainLocationId,
            Description = tour.Description,
            DurationDays = tour.DurationDays,
            DurationNights = tour.DurationNights,
            DurationMinutes = tour.DurationMinutes,
            BasePrice = tour.BasePrice,
            Status = ToDisplayStatus(tour.Status),
            IsItineraryLocked = await IsLockedAsync(tour, cancellationToken),
            ItineraryLockedAt = tour.ItineraryLockedAt
        };
    }

    public async Task UpdateTourAsync(Guid tourId, SaveTourRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            if (tour.DurationDays != request.DurationDays) await EnsureUnlockedAsync(tour, cancellationToken);
            Check(tour.Activities.All(a => a.DayNumber <= request.DurationDays), "durationDays",
                "Số ngày nhỏ hơn ngày của hoạt động đang có");
            if (tour.CategoryId != request.CategoryId) await EnsureCategoryAsync(request.CategoryId, cancellationToken);
            if (tour.MainLocationId != request.MainLocationId)
                await EnsureLocationAsync(request.MainLocationId, cancellationToken);

            var before = new
            {
                tour.Name,
                tour.CategoryId,
                tour.MainLocationId,
                tour.Description,
                tour.DurationDays,
                tour.DurationNights,
                tour.DurationMinutes,
                tour.BasePrice
            };
            tour.Name = request.Name.Trim();
            tour.CategoryId = request.CategoryId;
            tour.MainLocationId = request.MainLocationId;
            tour.Description = Trim(request.Description);
            tour.DurationDays = request.DurationDays;
            tour.DurationNights = request.DurationNights;
            tour.DurationMinutes = request.DurationMinutes;
            tour.BasePrice = request.BasePrice;
            tour.UpdatedAt = Now;
            await AuditAsync("Update", EntityType, tourId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
    }

    public async Task PublishTourAsync(Guid tourId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            Check(tour.Images.Any(i => i.IsPrimary), "images", "Tour cần có ảnh đại diện");
            Check(tour.Activities.Count > 0, "activities", "Tour cần ít nhất một hoạt động");
            Check(tour.ProvidedItems.Count > 0 || tour.PreparationItems.Count > 0, "items",
                "Tour cần có đồ cấp hoặc đồ tự mang");
            var before = tour.Status;
            // Lưu Active (không phải Published) vì danh sách khách đang lọc theo Active.
            tour.Status = TourStatuses.Active;
            tour.UpdatedAt = Now;
            await AuditAsync("Publish", EntityType, tourId, new { before, after = tour.Status }, cancellationToken);
        }, cancellationToken);
    }

    public async Task ArchiveTourAsync(Guid tourId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            var before = tour.Status;
            tour.Status = TourStatuses.Archived;
            tour.UpdatedAt = Now;
            await AuditAsync("Archive", EntityType, tourId, new { before, after = tour.Status }, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteTourAsync(Guid tourId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await UnitOfWork.Tours.GetByIdAsync(tourId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tour", ErrorCodes.NotFound);
            var versions = await UnitOfWork.Tours.GetFamilyVersionsAsync(tour.TourFamilyId,
                Clock.GetUtcNow(), cancellationToken);
            Check(versions.All(v => v.TripsCount == 0), "tourId", "Tour đã có chuyến, hãy lưu trữ thay vì xoá");
            // Xoá cả họ để không còn bản nào clone tiếp, tránh trùng version với bản đã xoá.
            foreach (var row in versions)
            {
                var version = await UnitOfWork.Tours.GetByIdAsync(row.TourId, cancellationToken);
                if (version is null) continue;
                version.IsDeleted = true;
                version.UpdatedAt = Now;
            }
            await AuditAsync("Delete", EntityType, tourId,
                new { tour.TourFamilyId, tour.Name, versions = versions.Select(v => v.Version) }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminTourImageResponse>> GetImagesAsync(Guid tourId, CancellationToken cancellationToken)
    {
        var tour = await LoadAsync(tourId, cancellationToken);
        return tour.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new AdminTourImageResponse
            {
                ImageId = i.Id,
                ImageUrl = i.ImageUrl,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary
            })
            .ToList();
    }

    public async Task<Guid> AddImageAsync(Guid tourId, SaveImageRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var imageId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            // Ảnh đầu tiên luôn là ảnh đại diện để tour luôn có ảnh đại diện khi đã có ảnh.
            var isPrimary = request.IsPrimary || tour.Images.Count == 0;
            if (isPrimary)
                foreach (var image in tour.Images) image.IsPrimary = false;
            await UnitOfWork.TourImages.AddAsync(new TourImage
            {
                Id = imageId,
                TourId = tourId,
                ImageUrl = request.ImageUrl.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsPrimary = isPrimary,
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            Touch(tour);
            await AuditAsync("AddImage", EntityType, tourId, new { imageId, request }, cancellationToken);
        }, cancellationToken);
        return imageId;
    }

    public async Task ReorderImagesAsync(Guid tourId, ReorderTourImagesRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            var images = tour.Images.ToDictionary(i => i.Id);
            Check(SameSet(request.ImageIdsInOrder, images.Keys), "imageIdsInOrder",
                "Danh sách phải gồm đúng mọi ảnh của tour");
            for (var index = 0; index < request.ImageIdsInOrder.Count; index++)
                images[request.ImageIdsInOrder[index]].DisplayOrder = index;
            if (request.PrimaryImageId is { } primaryId)
                foreach (var image in images.Values) image.IsPrimary = image.Id == primaryId;
            Touch(tour);
            await AuditAsync("ReorderImages", EntityType, tourId, request, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteImageAsync(Guid tourId, Guid imageId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            var image = tour.Images.FirstOrDefault(i => i.Id == imageId)
                ?? throw new NotFoundException("Không tìm thấy ảnh", ErrorCodes.NotFound);
            image.IsDeleted = true;
            image.UpdatedAt = Now;
            if (image.IsPrimary)
            {
                image.IsPrimary = false;
                var next = tour.Images.Where(i => !i.IsDeleted).OrderBy(i => i.DisplayOrder).FirstOrDefault();
                if (next is not null) next.IsPrimary = true;
            }
            Touch(tour);
            await AuditAsync("DeleteImage", EntityType, tourId, new { imageId, image.ImageUrl }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminTourActivityResponse>> GetActivitiesAsync(Guid tourId,
        CancellationToken cancellationToken)
    {
        var tour = await LoadAsync(tourId, cancellationToken);
        return tour.Activities
            .OrderBy(a => a.DayNumber).ThenBy(a => a.DisplayOrder).ThenBy(a => a.StartTime)
            .Select(a => new AdminTourActivityResponse
            {
                ActivityId = a.Id,
                DayNumber = a.DayNumber,
                DayTitle = a.DayTitle,
                StartTime = a.StartTime,
                DurationMinutes = a.DurationMinutes,
                Name = a.Name,
                Description = a.Description,
                DisplayOrder = a.DisplayOrder
            })
            .ToList();
    }

    public async Task<Guid> AddActivityAsync(Guid tourId, SaveTourActivityRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var activityId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            CheckDay(tour, request.DayNumber);
            await UnitOfWork.TourActivities.AddAsync(new TourActivity
            {
                Id = activityId,
                TourId = tourId,
                DayNumber = request.DayNumber,
                DayTitle = Trim(request.DayTitle),
                StartTime = request.StartTime,
                DurationMinutes = request.DurationMinutes,
                DisplayOrder = request.DisplayOrder ?? NextOrder(tour, request.DayNumber),
                Name = request.Name.Trim(),
                Description = Trim(request.Description),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            Touch(tour);
            await AuditAsync("AddActivity", EntityType, tourId, new { activityId, request }, cancellationToken);
        }, cancellationToken);
        return activityId;
    }

    public async Task UpdateActivityAsync(Guid tourId, Guid activityId, SaveTourActivityRequest request,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            var activity = tour.Activities.FirstOrDefault(a => a.Id == activityId)
                ?? throw new NotFoundException("Không tìm thấy hoạt động", ErrorCodes.NotFound);
            CheckDay(tour, request.DayNumber);
            var before = new
            {
                activity.DayNumber,
                activity.DayTitle,
                activity.StartTime,
                activity.DurationMinutes,
                activity.Name,
                activity.Description,
                activity.DisplayOrder
            };
            // Chuyển sang ngày khác mà không chỉ định thứ tự thì xếp cuối ngày mới.
            var order = request.DisplayOrder
                ?? (activity.DayNumber == request.DayNumber ? activity.DisplayOrder : NextOrder(tour, request.DayNumber));
            activity.DayNumber = request.DayNumber;
            activity.DayTitle = Trim(request.DayTitle);
            activity.StartTime = request.StartTime;
            activity.DurationMinutes = request.DurationMinutes;
            activity.Name = request.Name.Trim();
            activity.Description = Trim(request.Description);
            activity.DisplayOrder = order;
            activity.UpdatedAt = Now;
            Touch(tour);
            await AuditAsync("UpdateActivity", EntityType, tourId, new { activityId, before, after = request },
                cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteActivityAsync(Guid tourId, Guid activityId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            var activity = tour.Activities.FirstOrDefault(a => a.Id == activityId)
                ?? throw new NotFoundException("Không tìm thấy hoạt động", ErrorCodes.NotFound);
            activity.IsDeleted = true;
            activity.UpdatedAt = Now;
            Touch(tour);
            await AuditAsync("DeleteActivity", EntityType, tourId, new { activityId, activity.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task ReorderActivitiesAsync(Guid tourId, ReorderTourActivitiesRequest request,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            var activities = tour.Activities.Where(a => a.DayNumber == request.DayNumber).ToDictionary(a => a.Id);
            Check(SameSet(request.ActivityIdsInOrder, activities.Keys), "activityIdsInOrder",
                "Danh sách phải gồm đúng mọi hoạt động của ngày");
            for (var index = 0; index < request.ActivityIdsInOrder.Count; index++)
            {
                var activity = activities[request.ActivityIdsInOrder[index]];
                activity.DisplayOrder = index;
                activity.UpdatedAt = Now;
            }
            Touch(tour);
            await AuditAsync("ReorderActivities", EntityType, tourId, request, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminTourPreparationItemResponse>> GetPreparationItemsAsync(Guid tourId,
        CancellationToken cancellationToken)
    {
        var tour = await LoadAsync(tourId, cancellationToken);
        return tour.PreparationItems
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.ItemName)
            .Select(p => new AdminTourPreparationItemResponse
            {
                PreparationItemId = p.Id,
                ItemName = p.ItemName,
                Note = p.Note
            })
            .ToList();
    }

    public async Task SetPreparationItemsAsync(Guid tourId, TourPreparationItemsRequest request,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            var before = tour.PreparationItems.Select(p => new { p.ItemName, p.Note }).ToList();
            foreach (var item in tour.PreparationItems.ToList())
            {
                item.IsDeleted = true;
                item.UpdatedAt = Now;
            }
            await UnitOfWork.TourPreparationItems.AddRangeAsync(request.Items.Select(item => new TourPreparationItem
            {
                Id = Guid.NewGuid(),
                TourId = tourId,
                ItemName = item.ItemName.Trim(),
                Note = Trim(item.Note),
                CreatedAt = Now,
                UpdatedAt = Now
            }).ToList(), cancellationToken);
            Touch(tour);
            await AuditAsync("SetPreparationItems", EntityType, tourId, new { before, after = request.Items },
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminTourProvidedItemResponse>> GetProvidedItemsAsync(Guid tourId,
        CancellationToken cancellationToken)
    {
        var tour = await LoadAsync(tourId, cancellationToken);
        return tour.ProvidedItems
            .OrderBy(p => p.ItemName)
            .Select(p => new AdminTourProvidedItemResponse
            {
                TourProvidedItemId = p.Id,
                ProductVariantId = p.ProductVariantId,
                Sku = p.ProductVariant?.Sku,
                ProductName = p.ProductVariant?.Product.Name ?? p.ItemName,
                VariantName = p.ProductVariant?.Name,
                Quantity = p.Quantity,
                QuantityBasis = p.QuantityBasis,
                Note = p.Note
            })
            .ToList();
    }

    public async Task<Guid> AddProvidedItemAsync(Guid tourId, SaveTourProvidedItemRequest request,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        var itemId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            CheckVariantFree(tour, request.ProductVariantId, null);
            await UnitOfWork.TourProvidedItems.AddAsync(new TourProvidedItem
            {
                Id = itemId,
                TourId = tourId,
                ProductVariantId = request.ProductVariantId,
                ItemName = await VariantNameAsync(request.ProductVariantId, cancellationToken),
                Quantity = request.Quantity,
                QuantityBasis = request.QuantityBasis,
                Note = Trim(request.Note),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            Touch(tour);
            await AuditAsync("AddProvidedItem", EntityType, tourId, new { itemId, request }, cancellationToken);
        }, cancellationToken);
        return itemId;
    }

    public async Task UpdateProvidedItemAsync(Guid tourId, Guid itemId, SaveTourProvidedItemRequest request,
        CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            var item = tour.ProvidedItems.FirstOrDefault(p => p.Id == itemId)
                ?? throw new NotFoundException("Không tìm thấy đồ cấp", ErrorCodes.NotFound);
            CheckVariantFree(tour, request.ProductVariantId, itemId);
            var before = new { item.ProductVariantId, item.Quantity, item.QuantityBasis, item.Note };
            if (item.ProductVariantId != request.ProductVariantId)
            {
                item.ItemName = await VariantNameAsync(request.ProductVariantId, cancellationToken);
                item.ProductVariantId = request.ProductVariantId;
            }
            item.Quantity = request.Quantity;
            item.QuantityBasis = request.QuantityBasis;
            item.Note = Trim(request.Note);
            item.UpdatedAt = Now;
            Touch(tour);
            await AuditAsync("UpdateProvidedItem", EntityType, tourId, new { itemId, before, after = request },
                cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteProvidedItemAsync(Guid tourId, Guid itemId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            await EnsureUnlockedAsync(tour, cancellationToken);
            var item = tour.ProvidedItems.FirstOrDefault(p => p.Id == itemId)
                ?? throw new NotFoundException("Không tìm thấy đồ cấp", ErrorCodes.NotFound);
            item.IsDeleted = true;
            item.UpdatedAt = Now;
            Touch(tour);
            await AuditAsync("DeleteProvidedItem", EntityType, tourId, new { itemId, item.ItemName }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminTourVersionResponse>> GetVersionsAsync(Guid tourId,
        CancellationToken cancellationToken)
    {
        var tour = await UnitOfWork.Tours.GetByIdAsync(tourId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tour", ErrorCodes.NotFound);
        var rows = await UnitOfWork.Tours.GetFamilyVersionsAsync(tour.TourFamilyId, Clock.GetUtcNow(),
            cancellationToken);
        return rows.Select(r => new AdminTourVersionResponse
        {
            TourId = r.TourId,
            Version = r.Version,
            Status = ToDisplayStatus(r.Status),
            TripsCount = r.TripsCount,
            UpcomingTripsCount = r.UpcomingTripsCount,
            ItineraryLockedAt = r.ItineraryLockedAt,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<Guid> CloneVersionAsync(Guid tourId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var newId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var source = await LoadAsync(tourId, cancellationToken);
            // Tour chỉ xoá theo cả họ, nên max của các bản còn sống luôn là max thật (không đụng unique index).
            var versions = await UnitOfWork.Tours.GetFamilyVersionsAsync(source.TourFamilyId,
                Clock.GetUtcNow(), cancellationToken);
            var version = versions.Max(v => v.Version) + 1;
            await UnitOfWork.Tours.AddAsync(new Tour
            {
                Id = newId,
                TourFamilyId = source.TourFamilyId,
                Version = version,
                CreatedByUserId = ActorId,
                Name = source.Name,
                CategoryId = source.CategoryId,
                MainLocationId = source.MainLocationId,
                Description = source.Description,
                DifficultyLevel = source.DifficultyLevel,
                DurationDays = source.DurationDays,
                DurationNights = source.DurationNights,
                DurationMinutes = source.DurationMinutes,
                BasePrice = source.BasePrice,
                EquipmentDeposit = source.EquipmentDeposit,
                InspectionRate = source.InspectionRate,
                GroupMaxParticipants = source.GroupMaxParticipants,
                Status = TourStatuses.Draft,
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await UnitOfWork.TourImages.AddRangeAsync(source.Images.Select(i => new TourImage
            {
                Id = Guid.NewGuid(),
                TourId = newId,
                ImageUrl = i.ImageUrl,
                DisplayOrder = i.DisplayOrder,
                IsPrimary = i.IsPrimary,
                CreatedAt = Now,
                UpdatedAt = Now
            }).ToList(), cancellationToken);
            await UnitOfWork.TourActivities.AddRangeAsync(source.Activities.Select(a => new TourActivity
            {
                Id = Guid.NewGuid(),
                TourId = newId,
                DayNumber = a.DayNumber,
                DayTitle = a.DayTitle,
                StartTime = a.StartTime,
                DurationMinutes = a.DurationMinutes,
                DisplayOrder = a.DisplayOrder,
                Name = a.Name,
                Description = a.Description,
                CreatedAt = Now,
                UpdatedAt = Now
            }).ToList(), cancellationToken);
            await UnitOfWork.TourPreparationItems.AddRangeAsync(source.PreparationItems.Select(p => new TourPreparationItem
            {
                Id = Guid.NewGuid(),
                TourId = newId,
                ItemName = p.ItemName,
                Note = p.Note,
                CreatedAt = Now,
                UpdatedAt = Now
            }).ToList(), cancellationToken);
            await UnitOfWork.TourProvidedItems.AddRangeAsync(source.ProvidedItems.Select(p => new TourProvidedItem
            {
                Id = Guid.NewGuid(),
                TourId = newId,
                ProductVariantId = p.ProductVariantId,
                ItemName = p.ItemName,
                Quantity = p.Quantity,
                QuantityBasis = p.QuantityBasis,
                Note = p.Note,
                CreatedAt = Now,
                UpdatedAt = Now
            }).ToList(), cancellationToken);
            await UnitOfWork.TourPosts.AddRangeAsync(ActivePosts(source).Select(tp => new TourPost
            {
                TourPostId = Guid.NewGuid(),
                TourId = newId,
                PostId = tp.PostId,
                DisplayOrder = tp.DisplayOrder,
                CreatedAt = Now
            }).ToList(), cancellationToken);
            await AuditAsync("CloneVersion", EntityType, newId, new { sourceTourId = tourId, version },
                cancellationToken);
        }, cancellationToken);
        return newId;
    }

    public async Task<AdminTourDiffResponse> DiffVersionsAsync(Guid tourId, Guid compareWithVersionId,
        CancellationToken cancellationToken)
    {
        var source = await LoadAsync(tourId, cancellationToken);
        var target = await LoadAsync(compareWithVersionId, cancellationToken);
        Check(source.TourFamilyId == target.TourFamilyId, "compareWithVersionId", "Hai tour không cùng họ phiên bản");

        var changes = new List<AdminTourDiffItemResponse>();
        Diff(changes, "Info", InfoFields(source), InfoFields(target), f => f.Key, f => f.Value);
        Diff(changes, "Activities", source.Activities, target.Activities,
            a => $"Ngày {a.DayNumber} · {a.Name}",
            a => $"{a.StartTime:hh\\:mm}, {a.DurationMinutes} phút, thứ tự {a.DisplayOrder}, {a.DayTitle}, {a.Description}");
        Diff(changes, "ProvidedItems", source.ProvidedItems, target.ProvidedItems,
            p => p.ItemName, p => $"{p.Quantity} / {p.QuantityBasis}, {p.Note}");
        Diff(changes, "PreparationItems", source.PreparationItems, target.PreparationItems,
            p => p.ItemName, p => p.Note ?? string.Empty);
        Diff(changes, "Posts", ActivePosts(source), ActivePosts(target),
            tp => tp.Post.Title, tp => $"thứ tự {tp.DisplayOrder}");

        return new AdminTourDiffResponse
        {
            BaseTourId = source.Id,
            BaseVersion = source.Version,
            TargetTourId = target.Id,
            TargetVersion = target.Version,
            Changes = changes
        };
    }

    public async Task<IReadOnlyList<AdminTourPostResponse>> GetPostsAsync(Guid tourId, CancellationToken cancellationToken)
    {
        var tour = await LoadAsync(tourId, cancellationToken);
        return ActivePosts(tour)
            .Select(tp => new AdminTourPostResponse
            {
                PostId = tp.PostId,
                Title = tp.Post.Title,
                Slug = tp.Post.Slug,
                PostType = tp.Post.PostType,
                Status = tp.Post.Status,
                DisplayOrder = tp.DisplayOrder
            })
            .ToList();
    }

    public async Task SetPostsAsync(Guid tourId, TourPostsRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var tour = await LoadAsync(tourId, cancellationToken);
            var wanted = request.Items.ToDictionary(i => i.PostId, i => i.DisplayOrder);
            Check(await UnitOfWork.Posts.CountByIdsAsync(wanted.Keys.ToList(), cancellationToken) == wanted.Count,
                "items", "Có bài viết không tồn tại hoặc đã bị xoá");
            var before = ActivePosts(tour).Select(tp => new { tp.PostId, tp.DisplayOrder }).ToList();
            // TourPost không có cờ xoá mềm: bỏ gắn là xoá hẳn dòng liên kết (giống PostService).
            foreach (var link in tour.TourPosts.Where(tp => !wanted.ContainsKey(tp.PostId)).ToList())
            {
                tour.TourPosts.Remove(link);
                UnitOfWork.TourPosts.Remove(link);
            }
            foreach (var (postId, order) in wanted)
            {
                var link = tour.TourPosts.FirstOrDefault(tp => tp.PostId == postId);
                if (link is null)
                    tour.TourPosts.Add(new TourPost
                    {
                        TourPostId = Guid.NewGuid(),
                        TourId = tourId,
                        PostId = postId,
                        DisplayOrder = order,
                        CreatedAt = Now
                    });
                else
                    link.DisplayOrder = order;
            }
            Touch(tour);
            await AuditAsync("SetPosts", EntityType, tourId, new { before, after = request.Items }, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>Liên kết tới bài chưa xoá, theo thứ tự đọc (Post không có query filter xoá mềm).</summary>
    private static IEnumerable<TourPost> ActivePosts(Tour tour)
        => tour.TourPosts.Where(tp => !tp.Post.IsDeleted).OrderBy(tp => tp.DisplayOrder).ThenBy(tp => tp.Post.Title);

    /// <summary>Các trường thông tin chung đem so sánh, giá trị dạng chuỗi.</summary>
    private static (string Key, string Value)[] InfoFields(Tour tour) =>
    [
        ("name", tour.Name),
        ("categoryId", tour.CategoryId.ToString()),
        ("mainLocationId", tour.MainLocationId.ToString()),
        ("description", tour.Description ?? string.Empty),
        ("durationDays", tour.DurationDays.ToString(CultureInfo.InvariantCulture)),
        ("durationNights", tour.DurationNights.ToString(CultureInfo.InvariantCulture)),
        ("durationMinutes", tour.DurationMinutes.ToString(CultureInfo.InvariantCulture)),
        ("basePrice", tour.BasePrice.ToString(CultureInfo.InvariantCulture)),
        ("status", ToDisplayStatus(tour.Status))
    ];

    /// <summary>
    /// So hai danh sách theo khóa: chỉ có ở bản so sánh là Added, chỉ có ở bản gốc là Removed,
    /// có cả hai mà mô tả khác là Modified.
    /// </summary>
    private static void Diff<T>(List<AdminTourDiffItemResponse> changes, string section, IEnumerable<T> before,
        IEnumerable<T> after, Func<T, string> key, Func<T, string> describe)
    {
        // ponytail: khóa trùng (vd hai hoạt động cùng tên cùng ngày) chỉ so phần tử đầu; thêm id gốc khi clone nếu cần chính xác
        var baseItems = before.GroupBy(key).ToDictionary(g => g.Key, g => describe(g.First()));
        var targetItems = after.GroupBy(key).ToDictionary(g => g.Key, g => describe(g.First()));
        foreach (var itemKey in baseItems.Keys.Union(targetItems.Keys))
        {
            var inBase = baseItems.TryGetValue(itemKey, out var beforeValue);
            var inTarget = targetItems.TryGetValue(itemKey, out var afterValue);
            if (inBase && inTarget && beforeValue == afterValue) continue;
            changes.Add(new AdminTourDiffItemResponse
            {
                Section = section,
                Change = !inBase ? "Added" : !inTarget ? "Removed" : "Modified",
                Key = itemKey,
                Before = beforeValue,
                After = afterValue
            });
        }
    }

    /// <summary>Tour kèm cây con (tracked) hoặc 404.</summary>
    private async Task<Tour> LoadAsync(Guid tourId, CancellationToken cancellationToken)
        => await UnitOfWork.Tours.GetForEditAsync(tourId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tour", ErrorCodes.NotFound);

    /// <summary>Đã đóng dấu khóa hoặc đã có đơn đặt trên chuyến của tour.</summary>
    private async Task<bool> IsLockedAsync(Tour tour, CancellationToken cancellationToken)
        => tour.ItineraryLockedAt.HasValue || await UnitOfWork.Tours.HasBookingsAsync(tour.Id, cancellationToken);

    /// <summary>400 ITINERARY_LOCKED nếu lịch trình đã khóa.</summary>
    private async Task EnsureUnlockedAsync(Tour tour, CancellationToken cancellationToken)
    {
        if (await IsLockedAsync(tour, cancellationToken))
            throw new BadRequestException("Lịch trình tour đã bị khóa do đã có chuyến phát sinh người đặt mua.",
                ErrorCodes.ItineraryLocked);
    }

    /// <summary>Danh mục phải tồn tại và là danh mục tour.</summary>
    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await UnitOfWork.Categories.GetByIdAsync(categoryId, cancellationToken);
        Check(category is { IsDeleted: false, CategoryType: "TOUR" }, "categoryId",
            "Danh mục không tồn tại hoặc không phải danh mục tour");
    }

    /// <summary>Địa điểm chính phải tồn tại.</summary>
    private async Task EnsureLocationAsync(Guid locationId, CancellationToken cancellationToken)
    {
        var location = await UnitOfWork.Locations.GetByIdAsync(locationId, cancellationToken);
        Check(location is { IsDeleted: false }, "mainLocationId", "Địa điểm không tồn tại");
    }

    /// <summary>Tên đồ cấp "Sản phẩm - Biến thể"; biến thể phải tồn tại.</summary>
    private async Task<string> VariantNameAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var variant = await UnitOfWork.ProductVariants.GetByIdAsync(variantId, cancellationToken);
        Check(variant is { IsDeleted: false }, "productVariantId", "Biến thể không tồn tại");
        var product = await UnitOfWork.Products.GetByIdAsync(variant!.ProductId, cancellationToken);
        return product is null ? variant.Name : $"{product.Name} - {variant.Name}";
    }

    /// <summary>Mỗi biến thể chỉ một dòng định mức trong tour.</summary>
    private static void CheckVariantFree(Tour tour, Guid variantId, Guid? excludeItemId)
        => Check(!tour.ProvidedItems.Any(p => p.ProductVariantId == variantId && p.Id != excludeItemId),
            "productVariantId", "Biến thể đã có trong định mức đồ cấp");

    /// <summary>Ngày hoạt động phải nằm trong số ngày của tour.</summary>
    private static void CheckDay(Tour tour, int dayNumber)
        => Check(dayNumber <= tour.DurationDays, "dayNumber", $"Ngày phải từ 1 đến {tour.DurationDays}");

    /// <summary>Thứ tự kế tiếp (cuối ngày).</summary>
    private static int NextOrder(Tour tour, int dayNumber)
        => tour.Activities.Where(a => a.DayNumber == dayNumber && !a.IsDeleted)
            .Select(a => a.DisplayOrder).DefaultIfEmpty(-1).Max() + 1;

    /// <summary>Danh sách id trùng khớp đúng tập id hiện có (không thiếu, không thừa).</summary>
    private static bool SameSet(IReadOnlyCollection<Guid> ids, IReadOnlyCollection<Guid> existing)
        => ids.Count == existing.Count && ids.All(existing.Contains);

    /// <summary>Đổi trạng thái lưu sang trạng thái hiển thị như danh sách admin (Active → Published, Expired → Archived).</summary>
    private static string ToDisplayStatus(string status)
        => status == TourStatuses.Active ? TourStatuses.Published
            : status == TourStatuses.Expired ? TourStatuses.Archived : status;

    /// <summary>Đẩy UpdatedAt của tour để danh sách admin sắp theo lần sửa gần nhất.</summary>
    private void Touch(Tour tour) => tour.UpdatedAt = Now;

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
