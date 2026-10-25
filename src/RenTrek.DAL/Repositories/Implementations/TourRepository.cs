using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class TourRepository : GenericRepository<Tour>, ITourRepository
{
    public TourRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Tour> Items, int TotalCount)> GetPagedToursAsync(
        TourFilterParams filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.MainLocation)
            .Include(t => t.Images)
            .Include(t => t.Trips)
            .AsQueryable();

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

        await FillBookedSlotsAsync(items, cancellationToken);
        return (items, totalCount);
    }

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
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tour is not null)
            await FillBookedSlotsAsync([tour], cancellationToken);
        return tour;
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

        foreach (var trip in trips)
            trip.BookedSlots = booked.GetValueOrDefault(trip.TripId);
    }
}
