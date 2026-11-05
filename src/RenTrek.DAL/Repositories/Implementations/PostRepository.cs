using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public class PostRepository : GenericRepository<Post>, IPostRepository
{
    private const string Published = "Published";

    public PostRepository(AppDbContext context) : base(context)
    {
    }

    public Task<(IReadOnlyList<PostListRow> Items, int TotalCount)> GetPagedAsync(PostFilter filter, CancellationToken cancellationToken = default)
    {
        var posts = _dbSet.AsNoTracking().Where(p => !p.IsDeleted);
        if (filter.PublishedOnly)
            posts = posts.Where(p => p.Status == Published);
        else if (!string.IsNullOrWhiteSpace(filter.Status))
            posts = posts.Where(p => p.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.PostType))
            posts = posts.Where(p => p.PostType == filter.PostType);
        if (!string.IsNullOrWhiteSpace(filter.TagSlug))
            posts = posts.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == filter.TagSlug && !pt.Tag.IsDeleted));
        if (filter.SoftSkillId.HasValue)
            posts = posts.Where(p => p.PostSoftSkills.Any(ps => ps.SoftSkillId == filter.SoftSkillId.Value));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            posts = posts.Where(p => p.Title.ToLower().Contains(search));
        }

        posts = filter.PublishedOnly ? posts.OrderByDescending(p => p.PublishedAt) : posts.OrderByDescending(p => p.UpdatedAt);
        return posts.ToPageAsync(filter.PageNumber, filter.PageSize, ToListRow, cancellationToken);
    }

    public Task<List<PostListRow>> GetPublishedByTagsAsync(Guid excludePostId, IReadOnlyCollection<string> tagSlugs, int take, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == Published && p.PostId != excludePostId
                        && p.PostTags.Any(pt => tagSlugs.Contains(pt.Tag.Slug)))
            .OrderByDescending(p => p.PublishedAt)
            .Take(take)
            .Select(ToListRow)
            .ToListAsync(cancellationToken);

    public Task<Post?> GetDetailAsync(Guid postId, CancellationToken cancellationToken = default)
        => WithDetail().FirstOrDefaultAsync(p => p.PostId == postId, cancellationToken);

    public Task<Post?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => WithDetail().FirstOrDefaultAsync(p => p.Slug == slug && p.Status == Published, cancellationToken);

    public Task<Post?> GetForEditAsync(Guid postId, CancellationToken cancellationToken = default)
        => _dbSet
            .Include(p => p.PostTags)
            .Include(p => p.PostSoftSkills)
            .Include(p => p.TourPosts)
            .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, Guid excludePostId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(p => p.Slug == slug && p.PostId != excludePostId, cancellationToken);

    public async Task<(int Total, int Read)> GetReadStatsAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var tourIds = _context.TourPosts.Where(tp => tp.PostId == postId).Select(tp => tp.TourId);
        var participants = _context.BookingParticipants.AsNoTracking()
            .Where(bp => !bp.IsDeleted && bp.Status != "Cancelled" && tourIds.Contains(bp.Trip.TourId));
        var total = await participants.CountAsync(cancellationToken);
        var read = await participants.CountAsync(bp => bp.ParticipantPostReads.Any(r => r.PostId == postId), cancellationToken);
        return (total, read);
    }

    private IQueryable<Post> WithDetail()
        => _dbSet.AsNoTracking()
            .Include(p => p.CreatedByUser)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.PostSoftSkills).ThenInclude(ps => ps.SoftSkill)
            .Include(p => p.TourPosts).ThenInclude(tp => tp.Tour)
            .AsSplitQuery()
            .Where(p => !p.IsDeleted);

    private static readonly Expression<Func<Post, PostListRow>> ToListRow = p => new PostListRow
    {
        PostId = p.PostId,
        Title = p.Title,
        Slug = p.Slug,
        PostType = p.PostType,
        Status = p.Status,
        ContentPreview = p.Content.Length > PostListRow.PreviewLength ? p.Content.Substring(0, PostListRow.PreviewLength) : p.Content,
        PublishedAt = p.PublishedAt,
        UpdatedAt = p.UpdatedAt,
        CreatedByUserId = p.CreatedByUserId,
        AuthorName = p.CreatedByUser.FullName,
        Tags = p.PostTags.Where(pt => !pt.Tag.IsDeleted).OrderBy(pt => pt.Tag.Name).Select(pt => pt.Tag).ToList(),
        SoftSkills = p.PostSoftSkills.Where(ps => !ps.SoftSkill.IsDeleted).OrderBy(ps => ps.SoftSkill.Name).Select(ps => ps.SoftSkill).ToList(),
        TourCount = p.TourPosts.Count(tp => !tp.Tour.IsDeleted),
        ReadCount = p.ParticipantPostReads.Count
    };
}
