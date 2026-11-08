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
                GuideFullName = x.Participant.TripGroup == null || x.Participant.TripGroup.GuideUser == null ? null : x.Participant.TripGroup.GuideUser.FullName,
                IsHidden = x.IsHidden,
                HiddenReason = x.HiddenReason,
                CreatedAt = x.CreatedAt
            }, cancellationToken);
    }

    public Task<Review?> GetForUpdateAsync(Guid reviewId, CancellationToken cancellationToken = default)
        => _dbSet.SingleOrDefaultAsync(x => x.ReviewId == reviewId && !x.IsDeleted, cancellationToken);

    public Task<ParticipantReviewContextRow?> GetParticipantReviewContextAsync(
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _context.BookingParticipants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(bp =>
                bp.ParticipantId == participantId
                && !bp.IsDeleted
                && bp.LinkedUserId == userId
                && !bp.BookingOrder.Order.IsDeleted
                && !bp.Trip.IsDeleted)
            .Select(bp => new ParticipantReviewContextRow
            {
                ParticipantId = bp.ParticipantId,
                ParticipantStatus = bp.Status,
                TripStatus = bp.Trip.Status,
                OrderStatus = bp.BookingOrder.Order.Status,
                OrderPaymentStatus = bp.BookingOrder.Order.PaymentStatus,
                HasReview = _dbSet.Any(r => r.ParticipantId == bp.ParticipantId && !r.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Review?> GetByParticipantIdForUpdateAsync(
        Guid participantId,
        CancellationToken cancellationToken = default)
        => _dbSet.SingleOrDefaultAsync(x => x.ParticipantId == participantId && !x.IsDeleted, cancellationToken);

    public Task<Guid?> GetPublicTourFamilyIdAsync(Guid tourId, CancellationToken cancellationToken = default)
        => _context.Tours
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Id == tourId && !t.IsDeleted && t.Status != "Draft")
            .Select(t => (Guid?)t.TourFamilyId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TourReviewsSummaryRow> GetTourFamilyReviewsAsync(
        Guid tourFamilyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                !r.IsDeleted
                && !r.IsHidden
                && !r.Participant.IsDeleted
                && !r.Participant.Trip.IsDeleted
                && !r.Participant.Trip.Tour.IsDeleted
                && r.Participant.Trip.Tour.TourFamilyId == tourFamilyId);

        var ratingGroups = await baseQuery
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalCount = ratingGroups.Sum(x => x.Count);
        var ratingCounts = ratingGroups.ToDictionary(x => x.Rating, x => x.Count);

        IReadOnlyList<TourReviewRow> items = totalCount == 0
            ? []
            : await baseQuery
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.ReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new TourReviewRow
                {
                    ReviewId = r.ReviewId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    ReviewerName = r.Participant.FullName,
                    TripStartAt = r.Participant.Trip.StartAt
                })
                .ToListAsync(cancellationToken);

        return new TourReviewsSummaryRow
        {
            RatingCounts = ratingCounts,
            Items = items,
            TotalCount = totalCount
        };
    }
}
