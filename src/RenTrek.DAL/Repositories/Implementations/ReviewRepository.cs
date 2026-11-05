using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class ReviewRepository : GenericRepository<Review>, IReviewRepository
{
    public ReviewRepository(AppDbContext context) : base(context)
    {
    }

    public Task<(IReadOnlyList<AdminReviewRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminReviewFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(x => !x.IsDeleted && !x.Participant.IsDeleted);
        if (filter.Rating.HasValue) query = query.Where(x => x.Rating == filter.Rating);
        if (filter.TourId.HasValue) query = query.Where(x => x.Participant.Trip.TourId == filter.TourId);
        if (filter.GuideUserId.HasValue)
            query = query.Where(x => x.Participant.TripGroup != null && x.Participant.TripGroup.GuideUserId == filter.GuideUserId);
        if (filter.IsHidden.HasValue) query = query.Where(x => x.IsHidden == filter.IsHidden);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(x => x.Comment != null && x.Comment.ToLower().Contains(search));
        }

        return query
            .SortBy(filter.SortBy, filter.IsDescending, nameof(Review.CreatedAt), nameof(Review.Rating))
            .ToPageAsync(filter.PageNumber, filter.PageSize, x => new AdminReviewRow
            {
                ReviewId = x.ReviewId,
                Rating = x.Rating,
                Comment = x.Comment,
                CustomerFullName = x.Participant.FullName,
                TourName = x.Participant.Trip.Tour.Name,
                TripStartAt = x.Participant.Trip.StartAt,
                GuideFullName = x.Participant.TripGroup == null ? null : x.Participant.TripGroup.GuideUser.FullName,
                IsHidden = x.IsHidden,
                HiddenReason = x.HiddenReason,
                CreatedAt = x.CreatedAt
            }, cancellationToken);
    }

    public Task<Review?> GetForUpdateAsync(Guid reviewId, CancellationToken cancellationToken = default)
        => _dbSet.SingleOrDefaultAsync(x => x.ReviewId == reviewId && !x.IsDeleted, cancellationToken);
}
