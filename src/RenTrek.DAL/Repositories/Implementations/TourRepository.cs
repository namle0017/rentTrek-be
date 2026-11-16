using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho tour qua EF Core.
/// </summary>
public class TourRepository : GenericRepository<Tour>, ITourRepository
{
    public TourRepository(AppDbContext context) : base(context)
    {
    }

    public Task<int> CountByIdsAsync(IReadOnlyCollection<Guid> tourIds, CancellationToken cancellationToken = default)
        => _dbSet.CountAsync(t => tourIds.Contains(t.Id), cancellationToken);

    public Task<bool> AnyByMainLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(t => t.MainLocationId == locationId, cancellationToken);

    public Task<(IReadOnlyList<AdminTourRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminTourFilter filter,
        CancellationToken cancellationToken = default)
    {
        var now = filter.Now;
        var tours = _dbSet.AsNoTracking().Where(tour => !tour.IsDeleted);
        var latestVersions = tours
            .GroupBy(tour => tour.TourFamilyId)
            .Select(group => new { TourFamilyId = group.Key, Version = group.Max(tour => tour.Version) });

        var query = tours.Where(tour => latestVersions.Any(latest =>
            latest.TourFamilyId == tour.TourFamilyId && latest.Version == tour.Version));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(tour => tour.Name.ToLower().Contains(search));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(tour => tour.CategoryId == filter.CategoryId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(tour => tour.MainLocationId == filter.LocationId.Value);

        if (filter.Statuses is not null)
        {
            var statuses = filter.Statuses;
            query = query.Where(tour => statuses.Contains(tour.Status));
        }

        return query
            .OrderByDescending(tour => tour.UpdatedAt)
            .ThenBy(tour => tour.Id)
            .ToPageAsync(filter.PageNumber, filter.PageSize, tour => new AdminTourRow
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
                IsItineraryLocked = tour.ItineraryLockedAt.HasValue
                    || _context.TripBookingOrders.IgnoreQueryFilters().Any(b => b.Trip.TourId == tour.Id),
                Status = tour.Status,
                PrimaryImageUrl = tour.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault(),
                UpcomingTripsCount = tour.Trips.Count(trip => !trip.IsDeleted && trip.StartAt > now)
            }, cancellationToken);
    }

    public async Task<(IReadOnlyList<Tour> Items, int TotalCount)> GetPagedToursAsync(
        TourFilterParams filter,
        CancellationToken cancellationToken = default)
    {
        // Khách chỉ thấy tour đã xuất bản (Active/Published); Draft/Archived/Expired bị ẩn
        var sellable = _dbSet.Where(t => t.Status == "Active" || t.Status == "Published");
        var query = sellable
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.MainLocation)
            .Include(t => t.Images)
            .Include(t => t.Trips)
            // Mỗi họ tour chỉ 1 thẻ: bỏ bản nào còn bản đang bán khác cùng họ có Version lớn hơn.
            // Chuyến của bản cũ được gộp vào thẻ bản mới ở AddSellableFamilyTripsAsync bên dưới.
            .Where(t => !sellable.Any(newer => newer.TourFamilyId == t.TourFamilyId && newer.Version > t.Version));

        // Lọc theo Tab trạng thái: "Active" (Đang mở) hoặc "Expired" (Hết hạn)
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToLowerInvariant();
            if (status is "active" or "dang mo" or "đang mở")
            {
                query = query.Where(t => t.Status == "Active");
            }
            else if (status is "expired" or "het han" or "hết hạn")
            {
                query = query.Where(t => t.Status == "Expired");
            }
            else
            {
                query = query.Where(t => t.Status.ToLower() == status);
            }
        }

        // Tìm kiếm theo từ khóa
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(search) ||
                t.MainLocation.Name.ToLower().Contains(search) ||
                (t.Description != null && t.Description.ToLower().Contains(search)));
        }

        // Lọc theo Category
        if (filter.CategoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == filter.CategoryId.Value);
        }

        // Lọc theo Location
        if (filter.LocationId.HasValue)
        {
            query = query.Where(t => t.MainLocationId == filter.LocationId.Value);
        }

        // Lọc theo Độ khó (Dễ, Vừa phải, Thử thách, Khó)
        if (!string.IsNullOrWhiteSpace(filter.DifficultyLevel))
        {
            var difficulty = filter.DifficultyLevel.Trim().ToLower();
            query = query.Where(t => t.DifficultyLevel.ToLower() == difficulty);
        }

        // Lọc theo Số ngày
        if (filter.DurationDays.HasValue)
        {
            query = query.Where(t => t.DurationDays == filter.DurationDays.Value);
        }

        // Lọc theo Khoảng giá
        if (filter.MinPrice.HasValue)
        {
            query = query.Where(t => t.BasePrice >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(t => t.BasePrice <= filter.MaxPrice.Value);
        }

        // Sắp xếp
        query = filter.SortBy.ToLowerInvariant() switch
        {
            "price" => filter.IsDescending ? query.OrderByDescending(t => t.BasePrice) : query.OrderBy(t => t.BasePrice),
            "duration" => filter.IsDescending ? query.OrderByDescending(t => t.DurationDays) : query.OrderBy(t => t.DurationDays),
            "name" => filter.IsDescending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
            _ => filter.IsDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        await AddSellableFamilyTripsAsync(items, cancellationToken);
        await FillBookedSlotsAsync(items, cancellationToken);
        return (items, totalCount);
    }

    public Task<Tour?> GetForEditAsync(Guid tourId, CancellationToken cancellationToken = default)
        => _dbSet
            .Include(t => t.Images)
            .Include(t => t.Activities)
            .Include(t => t.PreparationItems)
            .Include(t => t.ProvidedItems).ThenInclude(p => p.ProductVariant!).ThenInclude(v => v.Product)
            .Include(t => t.TourPosts).ThenInclude(tp => tp.Post)
            .Include(t => t.SuggestedBundles).ThenInclude(sb => sb.Bundle)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == tourId, cancellationToken);

    public Task<Tour?> GetSuggestionsAsync(Guid tourId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Include(t => t.SuggestedBundles)
            .Include(t => t.ProvidedItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == tourId, cancellationToken);

    public Task<bool> HasBookingsAsync(Guid tourId, CancellationToken cancellationToken = default)
        => _context.TripBookingOrders.IgnoreQueryFilters().AnyAsync(b => b.Trip.TourId == tourId, cancellationToken);

    public async Task<IReadOnlyList<TourVersionRow>> GetFamilyVersionsAsync(
        Guid tourFamilyId, DateTimeOffset now, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Where(t => t.TourFamilyId == tourFamilyId)
            .OrderByDescending(t => t.Version)
            .Select(t => new TourVersionRow
            {
                TourId = t.Id,
                Version = t.Version,
                Status = t.Status,
                ItineraryLockedAt = t.ItineraryLockedAt,
                TripsCount = t.Trips.Count(trip => !trip.IsDeleted),
                UpcomingTripsCount = t.Trips.Count(trip => !trip.IsDeleted && trip.StartAt > now),
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

    public async Task<Tour?> GetTourDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tour = await _dbSet
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.MainLocation)
            .Include(t => t.Images)
            .Include(t => t.Activities)
            .Include(t => t.ProvidedItems)
            .Include(t => t.PreparationItems)
            .Include(t => t.Trips)
            .Include(t => t.TourPosts)
                .ThenInclude(tp => tp.Post)
            // 7 collection trong 1 câu JOIN nhân chéo số dòng (ảnh × hoạt động × chuyến × ...) gây treo khi tour có nhiều chuyến
            .AsSplitQuery()
            // Draft chưa xuất bản thì 404. Archived/Expired không hiện ở danh sách nhưng vẫn mở được
            // để khách xem lại chuyến đã đặt (FR-T06). Tour xoá mềm đã bị query filter ẩn.
            .FirstOrDefaultAsync(t => t.Id == id && t.Status != "Draft", cancellationToken);

        if (tour is null)
            return null;

        // Tour Archived/Expired chỉ để xem lại nên chỉ hiện chuyến của chính nó, không gộp chuyến bản khác
        if (tour.Status is "Active" or "Published")
            await AddSellableFamilyTripsAsync([tour], cancellationToken);
        await FillBookedSlotsAsync([tour], cancellationToken);
        return tour;
    }

    /// <summary>
    /// Gộp chuyến còn đặt được của các phiên bản khác cùng họ vào tour đang hiển thị.
    /// Ví dụ: v1 đang Active và còn chuyến 20/12 chưa chạy, admin vừa publish v2.
    /// Danh sách khách chỉ hiện thẻ v2 nên chuyến 20/12 phải đi kèm v2 thì khách mới đặt được.
    /// Chuyến vẫn giữ TourId của v1, tức khách đi theo lịch trình v1 (TripSummaryResponse.TourId cho FE biết điều này).
    /// Chỉ lấy chuyến Open, chưa khởi hành, của bản đang bán (Active/Published); bản Draft/Archived/Expired không gộp.
    /// Không lưu gì: tour được đọc AsNoTracking nên chỉ thêm vào bộ nhớ để map sang response.
    /// </summary>
    private async Task AddSellableFamilyTripsAsync(IReadOnlyCollection<Tour> tours, CancellationToken cancellationToken)
    {
        if (tours.Count == 0)
            return;

        var familyIds = tours.Select(t => t.TourFamilyId).Distinct().ToList();
        var shownIds = tours.Select(t => t.Id).ToList();
        var now = DateTimeOffset.UtcNow;
        var familyTrips = await _context.Trips
            .AsNoTracking()
            .Include(trip => trip.Tour)
            .Where(trip => familyIds.Contains(trip.Tour.TourFamilyId)
                && !shownIds.Contains(trip.TourId)
                && (trip.Tour.Status == "Active" || trip.Tour.Status == "Published")
                && trip.Status == "Open"
                && trip.StartAt > now)
            .ToListAsync(cancellationToken);

        foreach (var tour in tours)
        {
            foreach (var trip in familyTrips.Where(trip => trip.Tour.TourFamilyId == tour.TourFamilyId))
                tour.Trips.Add(trip);
        }
    }

    private async Task FillBookedSlotsAsync(IEnumerable<Tour> tours, CancellationToken cancellationToken)
    {
        var trips = tours.SelectMany(t => t.Trips).ToList();
        if (trips.Count == 0)
            return;

        var tripIds = trips.Select(t => t.TripId).ToList();
        var booked = await _context.TripBookingOrders
            .Where(x => tripIds.Contains(x.TripId))
            .Where(TripBookingRepository.HoldsSeats(DateTimeOffset.UtcNow))
            .GroupBy(x => x.TripId)
            .Select(g => new { TripId = g.Key, Count = g.Sum(x => x.ParticipantCount) })
            .ToDictionaryAsync(x => x.TripId, x => x.Count, cancellationToken);
        var held = await TripBookingRepository.RescheduleHolds(_context, DateTimeOffset.UtcNow)
            .Where(x => tripIds.Contains(x.ToTripId))
            .GroupBy(x => x.ToTripId)
            .Select(g => new { TripId = g.Key, Count = g.Sum(x => x.ParticipantCount) })
            .ToDictionaryAsync(x => x.TripId, x => x.Count, cancellationToken);

        foreach (var trip in trips)
            trip.BookedSlots = booked.GetValueOrDefault(trip.TripId) + held.GetValueOrDefault(trip.TripId);
    }
}
