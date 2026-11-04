using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Post.Requests;
using RenTrek.BLL.DTOs.Post.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;
using PostEntity = RenTrek.DAL.Entities.Post;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Nghiệp vụ bài viết / cẩm nang (FR-N01–N03): soạn, xuất bản và hiển thị công khai.
/// </summary>
public class PostService : IPostService
{
    private const int ExcerptLength = 220;
    private const int SameTagPostLimit = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public PostService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    // ---------- Công khai ----------

    public async Task<PagedResponse<PostListItemResponse>> GetPublishedPostsAsync(
        PostQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var result = await ApplyFilters(PublishedPosts(), query)
            .OrderByDescending(p => p.PublishedAt)
            .Select(ToListItem)
            .ToPagedResponseAsync(query, cancellationToken);

        foreach (var item in result.Items)
            item.Excerpt = MakeExcerpt(item.Excerpt);

        return result;
    }

    public async Task<PostDetailResponse> GetPublishedPostBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var post = await PublishedPosts()
            .Where(p => p.Slug == normalizedSlug)
            .Select(p => new PostDetailResponse
            {
                PostId = p.PostId,
                Title = p.Title,
                Slug = p.Slug,
                PostType = p.PostType,
                Content = p.Content,
                PublishedAt = p.PublishedAt,
                AuthorName = p.CreatedByUser.FullName,
                Tags = p.PostTags
                    .Where(pt => !pt.Tag.IsDeleted)
                    .OrderBy(pt => pt.Tag.Name)
                    .Select(pt => new PostTagResponse(pt.Tag.Name, pt.Tag.Slug))
                    .ToList(),
                SoftSkills = p.PostSoftSkills
                    .Where(ps => !ps.SoftSkill.IsDeleted)
                    .OrderBy(ps => ps.SoftSkill.Name)
                    .Select(ps => new PostSoftSkillResponse(ps.SoftSkillId, ps.SoftSkill.Name))
                    .ToList(),
                RelatedTours = p.TourPosts
                    .Where(tp => !tp.Tour.IsDeleted)
                    .OrderBy(tp => tp.DisplayOrder)
                    .Select(tp => new PostTourResponse(
                        tp.TourId, tp.Tour.Name, tp.Tour.DurationDays, tp.Tour.DurationNights, tp.Tour.BasePrice))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài viết.");

        var tagSlugs = post.Tags.Select(t => t.Slug).ToList();
        if (tagSlugs.Count > 0)
        {
            var sameTagPosts = await PublishedPosts()
                .Where(p => p.PostId != post.PostId && p.PostTags.Any(pt => tagSlugs.Contains(pt.Tag.Slug)))
                .OrderByDescending(p => p.PublishedAt)
                .Take(SameTagPostLimit)
                .Select(ToListItem)
                .ToListAsync(cancellationToken);

            sameTagPosts.ForEach(item => item.Excerpt = MakeExcerpt(item.Excerpt));
            post.SameTagPosts = sameTagPosts;
        }

        return post;
    }

    public async Task<PostTaxonomyResponse> GetTaxonomyAsync(CancellationToken cancellationToken = default)
    {
        var tags = await _unitOfWork.Tags.Query()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .Select(t => new PostTagResponse(t.Name, t.Slug))
            .ToListAsync(cancellationToken);

        var softSkills = await _unitOfWork.SoftSkills.Query()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new PostSoftSkillResponse(s.SoftSkillId, s.Name))
            .ToListAsync(cancellationToken);

        return new PostTaxonomyResponse(tags, softSkills);
    }

    // ---------- Vận hành ----------

    public Task<PagedResponse<AdminPostListItemResponse>> GetAdminPostsAsync(
        AdminPostQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequiredUserId;
        var isAdmin = IsAdmin;

        var posts = ApplyFilters(_unitOfWork.Posts.Query().Where(p => !p.IsDeleted), query);
        if (!string.IsNullOrWhiteSpace(query.Status))
            posts = posts.Where(p => p.Status == query.Status);

        return posts
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new AdminPostListItemResponse
            {
                PostId = p.PostId,
                Title = p.Title,
                Slug = p.Slug,
                PostType = p.PostType,
                Status = p.Status,
                PublishedAt = p.PublishedAt,
                UpdatedAt = p.UpdatedAt,
                AuthorName = p.CreatedByUser.FullName,
                TourCount = p.TourPosts.Count(tp => !tp.Tour.IsDeleted),
                ReadCount = p.ParticipantPostReads.Count,
                CanEdit = isAdmin || p.CreatedByUserId == userId
            })
            .ToPagedResponseAsync(query, cancellationToken);
    }

    public async Task<AdminPostDetailResponse> GetAdminPostByIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequiredUserId;
        var isAdmin = IsAdmin;

        return await _unitOfWork.Posts.Query()
            .Where(p => p.PostId == postId && !p.IsDeleted)
            .Select(p => new AdminPostDetailResponse
            {
                PostId = p.PostId,
                Title = p.Title,
                Slug = p.Slug,
                PostType = p.PostType,
                Content = p.Content,
                Status = p.Status,
                PublishedAt = p.PublishedAt,
                AuthorName = p.CreatedByUser.FullName,
                TagNames = p.PostTags
                    .Where(pt => !pt.Tag.IsDeleted)
                    .OrderBy(pt => pt.Tag.Name)
                    .Select(pt => pt.Tag.Name)
                    .ToList(),
                SoftSkillIds = p.PostSoftSkills.Select(ps => ps.SoftSkillId).ToList(),
                Tours = p.TourPosts
                    .Where(tp => !tp.Tour.IsDeleted)
                    .OrderBy(tp => tp.DisplayOrder)
                    .Select(tp => new PostTourResponse(
                        tp.TourId, tp.Tour.Name, tp.Tour.DurationDays, tp.Tour.DurationNights, tp.Tour.BasePrice))
                    .ToList(),
                CanEdit = isAdmin || p.CreatedByUserId == userId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài viết.");
    }

    public async Task<AdminPostDetailResponse> CreateAsync(UpsertPostRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var post = new PostEntity
        {
            PostId = Guid.NewGuid(),
            CreatedByUserId = _currentUser.RequiredUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await ApplyRequestAsync(post, request, now, cancellationToken);
        await _unitOfWork.Posts.AddAsync(post, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAdminPostByIdAsync(post.PostId, cancellationToken);
    }

    public async Task<AdminPostDetailResponse> UpdateAsync(
        Guid postId,
        UpsertPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await _unitOfWork.Posts.QueryTracking()
            .Include(p => p.PostTags)
            .Include(p => p.PostSoftSkills)
            .Include(p => p.TourPosts)
            .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài viết.");

        EnsureCanEdit(post);

        var now = DateTime.UtcNow;
        await ApplyRequestAsync(post, request, now, cancellationToken);
        post.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAdminPostByIdAsync(post.PostId, cancellationToken);
    }

    public async Task DeleteAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _unitOfWork.Posts.QueryTracking()
            .FirstOrDefaultAsync(p => p.PostId == postId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài viết.");

        EnsureCanEdit(post);

        // Soft delete: giữ slug và lịch sử đọc (BR-05)
        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------- Nội bộ ----------

    private bool IsAdmin => _currentUser.Role == RoleNames.Admin;

    private void EnsureCanEdit(PostEntity post)
    {
        if (!IsAdmin && post.CreatedByUserId != _currentUser.RequiredUserId)
            throw new ForbiddenException("Bạn chỉ được sửa bài viết do mình tạo.", ErrorCodes.Forbidden);
    }

    private IQueryable<PostEntity> PublishedPosts()
        => _unitOfWork.Posts.Query().Where(p => !p.IsDeleted && p.Status == PostStatuses.Published);

    private static IQueryable<PostEntity> ApplyFilters(IQueryable<PostEntity> posts, PostQueryRequest query)
    {
        if (!string.IsNullOrWhiteSpace(query.PostType))
            posts = posts.Where(p => p.PostType == query.PostType);

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tagSlug = query.Tag.Trim().ToLowerInvariant();
            posts = posts.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tagSlug && !pt.Tag.IsDeleted));
        }

        if (query.SoftSkillId.HasValue)
            posts = posts.Where(p => p.PostSoftSkills.Any(ps => ps.SoftSkillId == query.SoftSkillId.Value));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            posts = posts.Where(p => p.Title.ToLower().Contains(search));
        }

        return posts;
    }

    private static readonly Expression<Func<PostEntity, PostListItemResponse>> ToListItem = p => new PostListItemResponse
    {
        PostId = p.PostId,
        Title = p.Title,
        Slug = p.Slug,
        PostType = p.PostType,
        // Cắt thô ở DB để không kéo cả nội dung dài; MakeExcerpt làm gọn sau
        Excerpt = p.Content.Length > ExcerptLength * 2 ? p.Content.Substring(0, ExcerptLength * 2) : p.Content,
        PublishedAt = p.PublishedAt,
        AuthorName = p.CreatedByUser.FullName,
        Tags = p.PostTags
            .Where(pt => !pt.Tag.IsDeleted)
            .OrderBy(pt => pt.Tag.Name)
            .Select(pt => new PostTagResponse(pt.Tag.Name, pt.Tag.Slug))
            .ToList(),
        SoftSkills = p.PostSoftSkills
            .Where(ps => !ps.SoftSkill.IsDeleted)
            .OrderBy(ps => ps.SoftSkill.Name)
            .Select(ps => new PostSoftSkillResponse(ps.SoftSkillId, ps.SoftSkill.Name))
            .ToList()
    };

    /// <summary>Bỏ cú pháp Markdown (tiêu đề, ảnh, in đậm...), gộp khoảng trắng, cắt theo từ.</summary>
    private static string MakeExcerpt(string content)
    {
        var text = MarkdownHelper.ToPlainText(content);

        if (text.Length <= ExcerptLength)
            return text;

        var cut = text.LastIndexOf(' ', ExcerptLength);
        return text[..(cut > 0 ? cut : ExcerptLength)] + "…";
    }

    private async Task ApplyRequestAsync(
        PostEntity post,
        UpsertPostRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var slug = SlugHelper.Slugify(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);
        if (slug.Length == 0)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["slug"] = ["Đường dẫn phải chứa ít nhất một chữ cái hoặc chữ số."]
            });

        // DB unique gồm cả bài đã xóa mềm
        if (await _unitOfWork.Posts.AnyAsync(p => p.Slug == slug && p.PostId != post.PostId, cancellationToken))
            throw new ConflictException("Đường dẫn đã được dùng bởi bài khác.", errors: new Dictionary<string, string[]>
            {
                ["slug"] = ["Đường dẫn đã được dùng bởi bài khác. Thêm hậu tố, ví dụ “-2”."]
            });

        post.Title = request.Title.Trim();
        post.Slug = slug;
        post.PostType = request.PostType;
        post.Content = request.Content.Trim();
        post.Status = request.Status;
        if (request.Status == PostStatuses.Published)
            post.PublishedAt ??= now;

        await SyncTagsAsync(post, request.TagNames, now, cancellationToken);
        await SyncSoftSkillsAsync(post, request.SoftSkillIds, now, cancellationToken);
        await SyncToursAsync(post, request.TourIds, now, cancellationToken);
    }

    private async Task SyncTagsAsync(PostEntity post, List<string> tagNames, DateTime now, CancellationToken cancellationToken)
    {
        var wanted = tagNames
            .Select(name => name.Trim())
            .Select(name => (Name: name, Slug: SlugHelper.Slugify(name)))
            .Where(t => t.Slug.Length > 0)
            .DistinctBy(t => t.Slug)
            .ToList();
        var wantedSlugs = wanted.Select(t => t.Slug).ToList();

        var tags = await _unitOfWork.Tags.QueryTracking()
            .Where(t => wantedSlugs.Contains(t.Slug))
            .ToListAsync(cancellationToken);

        foreach (var tag in tags.Where(t => t.IsDeleted))
        {
            tag.IsDeleted = false;
            tag.UpdatedAt = now;
        }

        // Tạo nhanh tag chưa có
        foreach (var (name, slug) in wanted.Where(w => tags.All(t => t.Slug != w.Slug)))
        {
            var tag = new Tag { TagId = Guid.NewGuid(), Name = name, Slug = slug, CreatedAt = now, UpdatedAt = now };
            await _unitOfWork.Tags.AddAsync(tag, cancellationToken);
            tags.Add(tag);
        }

        var wantedTagIds = tags.Select(t => t.TagId).ToHashSet();
        foreach (var link in post.PostTags.Where(pt => !wantedTagIds.Contains(pt.TagId)).ToList())
        {
            post.PostTags.Remove(link);
            _unitOfWork.PostTags.Remove(link);
        }

        foreach (var tag in tags.Where(t => post.PostTags.All(pt => pt.TagId != t.TagId)))
            post.PostTags.Add(new PostTag { PostTagId = Guid.NewGuid(), PostId = post.PostId, TagId = tag.TagId, Tag = tag, CreatedAt = now });
    }

    private async Task SyncSoftSkillsAsync(PostEntity post, List<Guid> softSkillIds, DateTime now, CancellationToken cancellationToken)
    {
        var wantedIds = softSkillIds.Distinct().ToList();
        var existingCount = await _unitOfWork.SoftSkills.Query()
            .CountAsync(s => wantedIds.Contains(s.SoftSkillId) && !s.IsDeleted, cancellationToken);
        if (existingCount != wantedIds.Count)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["softSkillIds"] = ["Có kỹ năng không tồn tại hoặc đã bị xóa."]
            });

        foreach (var link in post.PostSoftSkills.Where(ps => !wantedIds.Contains(ps.SoftSkillId)).ToList())
        {
            post.PostSoftSkills.Remove(link);
            _unitOfWork.PostSoftSkills.Remove(link);
        }

        foreach (var id in wantedIds.Where(id => post.PostSoftSkills.All(ps => ps.SoftSkillId != id)))
            post.PostSoftSkills.Add(new PostSoftSkill { PostSoftSkillId = Guid.NewGuid(), PostId = post.PostId, SoftSkillId = id, CreatedAt = now });
    }

    private async Task SyncToursAsync(PostEntity post, List<Guid> tourIds, DateTime now, CancellationToken cancellationToken)
    {
        var wantedIds = tourIds.Distinct().ToList();
        var existingCount = await _unitOfWork.Tours.Query()
            .CountAsync(t => wantedIds.Contains(t.Id), cancellationToken);
        if (existingCount != wantedIds.Count)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["tourIds"] = ["Có tour không tồn tại hoặc đã bị xóa."]
            });

        foreach (var link in post.TourPosts.Where(tp => !wantedIds.Contains(tp.TourId)).ToList())
        {
            post.TourPosts.Remove(link);
            _unitOfWork.TourPosts.Remove(link);
        }

        for (var order = 0; order < wantedIds.Count; order++)
        {
            var link = post.TourPosts.FirstOrDefault(tp => tp.TourId == wantedIds[order]);
            if (link is null)
                post.TourPosts.Add(new TourPost { TourPostId = Guid.NewGuid(), PostId = post.PostId, TourId = wantedIds[order], DisplayOrder = order, CreatedAt = now });
            else
                link.DisplayOrder = order;
        }
    }
}
