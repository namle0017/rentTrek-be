using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy vấn danh sách tham gia và lịch sử đọc bài viết của hành khách qua EF Core.
/// </summary>
public class ParticipantPostReadRepository : GenericRepository<ParticipantPostRead>, IParticipantPostReadRepository
{
    private const string Cancelled = "Cancelled";
    private const string Published = "Published";

    public ParticipantPostReadRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<MyParticipationRow>> GetMyParticipationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _context.BookingParticipants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(bp =>
                !bp.IsDeleted
                && bp.Status != Cancelled
                && bp.LinkedUserId == userId
                && !bp.BookingOrder.Order.IsDeleted
                && !bp.Trip.IsDeleted)
            .OrderByDescending(bp => bp.CreatedAt)
            .ThenByDescending(bp => bp.ParticipantId)
            .Select(bp => new MyParticipationRow
            {
                ParticipantId = bp.ParticipantId,
                TripId = bp.TripId,
                TourId = bp.Trip.TourId,
                TourName = bp.Trip.Tour.Name,
                StartAt = bp.Trip.StartAt,
                EndAt = bp.Trip.EndAt,
                TripStatus = bp.Trip.Status,
                ParticipantStatus = bp.Status,
                OrderStatus = bp.BookingOrder.Order.Status,
                OrderPaymentStatus = bp.BookingOrder.Order.PaymentStatus,
                UnreadPostCount = _context.TourPosts.Count(tp =>
                    tp.TourId == bp.Trip.TourId
                    && !tp.Post.IsDeleted
                    && tp.Post.Status == Published
                    && !_context.ParticipantPostReads.Any(r =>
                        r.ParticipantId == bp.ParticipantId && r.PostId == tp.PostId)),
                HasReview = _context.Reviews.Any(r =>
                    r.ParticipantId == bp.ParticipantId && !r.IsDeleted),
                OrderId = bp.BookingOrder.Order.OrderId,
                ReviewId = _context.Reviews
                    .Where(r => r.ParticipantId == bp.ParticipantId && !r.IsDeleted)
                    .Select(r => (Guid?)r.ReviewId).FirstOrDefault(),
                ReviewRating = _context.Reviews
                    .Where(r => r.ParticipantId == bp.ParticipantId && !r.IsDeleted)
                    .Select(r => (int?)r.Rating).FirstOrDefault(),
                ReviewComment = _context.Reviews
                    .Where(r => r.ParticipantId == bp.ParticipantId && !r.IsDeleted)
                    .Select(r => r.Comment).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

    public Task<bool> IsOwnedActiveParticipantAsync(
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _context.BookingParticipants
            .IgnoreQueryFilters()
            .AnyAsync(bp =>
                bp.ParticipantId == participantId
                && !bp.IsDeleted
                && bp.Status != Cancelled
                && bp.LinkedUserId == userId
                && !bp.BookingOrder.Order.IsDeleted
                && !bp.Trip.IsDeleted,
                cancellationToken);

    public async Task<IReadOnlyList<ParticipantPostRow>> GetParticipantPostsAsync(
        Guid participantId,
        CancellationToken cancellationToken = default)
        => await _context.TourPosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tp =>
                !tp.Post.IsDeleted
                && tp.Post.Status == Published
                && _context.BookingParticipants.IgnoreQueryFilters().Any(bp =>
                    bp.ParticipantId == participantId
                    && !bp.IsDeleted
                    && bp.Status != Cancelled
                    && !bp.BookingOrder.Order.IsDeleted
                    && !bp.Trip.IsDeleted
                    && bp.Trip.TourId == tp.TourId))
            .OrderBy(tp => tp.DisplayOrder)
            .ThenBy(tp => tp.Post.Title)
            .ThenBy(tp => tp.PostId)
            .Select(tp => new ParticipantPostRow
            {
                PostId = tp.PostId,
                Title = tp.Post.Title,
                Slug = tp.Post.Slug,
                ReadAt = _context.ParticipantPostReads
                    .Where(r => r.ParticipantId == participantId && r.PostId == tp.PostId)
                    .Select(r => (DateTime?)r.ReadAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

    public Task<bool> CanAccessTourPostAsync(
        Guid participantId,
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
        => _context.BookingParticipants
            .IgnoreQueryFilters()
            .AnyAsync(bp =>
                bp.ParticipantId == participantId
                && !bp.IsDeleted
                && bp.Status != Cancelled
                && bp.LinkedUserId == userId
                && !bp.BookingOrder.Order.IsDeleted
                && !bp.Trip.IsDeleted
                && _context.TourPosts.Any(tp =>
                    tp.TourId == bp.Trip.TourId
                    && tp.PostId == postId
                    && !tp.Post.IsDeleted
                    && tp.Post.Status == Published),
                cancellationToken);

    public Task<ParticipantPostRead?> GetByParticipantAndPostAsync(
        Guid participantId,
        Guid postId,
        CancellationToken cancellationToken = default)
        => _dbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.ParticipantId == participantId && r.PostId == postId, cancellationToken);
}
