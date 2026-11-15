using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Post.Requests;
using RenTrek.BLL.DTOs.Post.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;
using PostEntity = RenTrek.DAL.Entities.Post;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Nghiệp vụ bài viết / cẩm nang (FR-N01–N03): soạn, xuất bản và hiển thị công khai.
/// </summary>
/// <remarks>
/// Gộp luồng trùng (2026-11-05, Tri Tin refactor, commit 629189a): bỏ /admin/management/posts
/// (AdminPostsController trong Controllers/Admin + phần post của AdminService, do TIGER.ER viết, commit 6cf600b "Task Admin").
/// </remarks>
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
        var (items, total) = await _unitOfWork.Posts.GetPagedAsync(ToFilter(query, publishedOnly: true), cancellationToken);
        return ToPaged(query, total, items.Select(ToListItem).ToList());
    }

    public async Task<PostDetailResponse> GetPublishedPostBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var p = await _unitOfWork.Posts.GetPublishedBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài viết.");

        var post = new PostDetailResponse
        {
            PostId = p.PostId,
            Title = p.Title,
            Slug = p.Slug,
            PostType = p.PostType,
            Content = p.Content,
            CoverImageUrl = MarkdownHelper.FirstImageUrl(p.Content),
            PublishedAt = p.PublishedAt,
            AuthorName = p.CreatedByUser?.FullName ?? string.Empty,
            Tags = ActiveTags(p).Select(t => new PostTagResponse(t.Name, t.Slug)).ToList(),
            SoftSkills = p.PostSoftSkills
                .Where(ps => !ps.SoftSkill.IsDeleted)
                .OrderBy(ps => ps.SoftSkill.Name)
                .Select(ps => new PostSoftSkillResponse(ps.SoftSkillId, ps.SoftSkill.Name))
                .ToList(),
            RelatedTours = ActiveTours(p, publishedOnly: true)
        };

        var tagSlugs = post.Tags.Select(t => t.Slug).ToList();
        if (tagSlugs.Count > 0)
        {
            var sameTagPosts = await _unitOfWork.Posts.GetPublishedByTagsAsync(post.PostId, tagSlugs, SameTagPostLimit, cancellationToken);
            post.SameTagPosts = sameTagPosts.Select(ToListItem).ToList();
        }

        return post;
    }

    public async Task<PostTaxonomyResponse> GetTaxonomyAsync(CancellationToken cancellationToken = default)
    {
        var tags = await _unitOfWork.Tags.GetActiveAsync(cancellationToken);
        var softSkills = await _unitOfWork.SoftSkills.GetActiveAsync(cancellationToken);

        return new PostTaxonomyResponse(
            tags.Select(t => new PostTagResponse(t.Name, t.Slug, t.TagId)).ToList(),
            softSkills.Select(s => new PostSoftSkillResponse(s.SoftSkillId, s.Name)).ToList());
    }

    // ---------- Vận hành ----------

    public async Task<PagedResponse<AdminPostListItemResponse>> GetAdminPostsAsync(
        AdminPostQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequiredUserId;
        var isAdmin = IsAdmin;

        var filter = ToFilter(query, publishedOnly: false);
        filter.Status = query.Status;
        var (items, total) = await _unitOfWork.Posts.GetPagedAsync(filter, cancellationToken);

        return ToPaged(query, total, items.Select(p => new AdminPostListItemResponse
        {
            PostId = p.PostId,
            Title = p.Title,
            Slug = p.Slug,
            PostType = p.PostType,
            Status = p.Status,
            PublishedAt = p.PublishedAt,
            UpdatedAt = p.UpdatedAt,
            AuthorName = p.AuthorName,
            TourCount = p.TourCount,
            ReadCount = p.ReadCount,
            CanEdit = isAdmin || p.CreatedByUserId == userId
        }).ToList());
    }

    public async Task<AdminPostDetailResponse> GetAdminPostByIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var p = await _unitOfWork.Posts.GetDetailAsync(postId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài viết.");

        return new AdminPostDetailResponse
        {
            PostId = p.PostId,
            Title = p.Title,
            Slug = p.Slug,
            PostType = p.PostType,
            Content = p.Content,
            Status = p.Status,
            PublishedAt = p.PublishedAt,
            AuthorName = p.CreatedByUser?.FullName ?? string.Empty,
            TagIds = p.PostTags.Where(pt => !pt.Tag.IsDeleted).Select(pt => pt.TagId).ToList(),
            SoftSkillIds = p.PostSoftSkills.Select(ps => ps.SoftSkillId).ToList(),
            Tours = ActiveTours(p),
            CanEdit = IsAdmin || p.CreatedByUserId == _currentUser.RequiredUserId
        };
    }

    public async Task<AdminPostDetailResponse> CreateAsync(UpsertPostRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var post = new PostEntity
        {
            PostId = Guid.NewGuid(),
            CreatedByUserId = _currentUser.RequiredUserId,
            Status = PostStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        await ApplyRequestAsync(post, request, now, cancellationToken);
        await _unitOfWork.Posts.AddAsync(post, cancellationToken);
        await AuditAsync("Create", post.PostId, new { after = request }, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAdminPostByIdAsync(post.PostId, cancellationToken);
    }

    public async Task<AdminPostDetailResponse> UpdateAsync(
        Guid postId,
        UpsertPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await _unitOfWork.Posts.GetForEditAsync(postId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy bài viết.");

        EnsureCanEdit(post);

        var now = DateTime.UtcNow;
        var before = new { post.Title, post.Slug, post.PostType, post.Content };
        await ApplyRequestAsync(post, request, now, cancellationToken);
        post.UpdatedAt = now;
        await AuditAsync("Update", post.PostId, new { before, after = request }, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAdminPostByIdAsync(post.PostId, cancellationToken);
    }

    public Task<AdminPostDetailResponse> PublishAsync(Guid postId, CancellationToken cancellationToken = default)
        => SetStatusAsync(postId, PostStatuses.Published, cancellationToken);

    public Task<AdminPostDetailResponse> ArchiveAsync(Guid postId, CancellationToken cancellationToken = default)
        => SetStatusAsync(postId, PostStatuses.Archived, cancellationToken);

    /// <summary>Chỉ Admin (chặn ở controller); Staff dùng archive để ẩn bài.</summary>
    public async Task DeleteAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await FindTrackedAsync(postId, cancellationToken);

        // Soft delete: giữ slug và lịch sử đọc (BR-05)
        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        await AuditAsync("Delete", post.PostId, new { before = post.Status }, post.UpdatedAt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PostReadAnalyticsResponse> GetReadAnalyticsAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        await FindTrackedAsync(postId, cancellationToken);
        var (total, read) = await _unitOfWork.Posts.GetReadStatsAsync(postId, cancellationToken);

        return new PostReadAnalyticsResponse(postId, total, read, total == 0 ? 0 : Math.Round(100m * read / total, 2));
    }

    // ---------- Nội bộ ----------

    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    private async Task<AdminPostDetailResponse> SetStatusAsync(Guid postId, string status, CancellationToken cancellationToken)
    {
        var post = await FindTrackedAsync(postId, cancellationToken);
        EnsureCanEdit(post);

        var before = post.Status;
        var now = DateTime.UtcNow;
        post.Status = status;
        if (status == PostStatuses.Published)
            post.PublishedAt ??= now;
        post.UpdatedAt = now;
        await AuditAsync("Update", post.PostId, new { before, after = status }, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAdminPostByIdAsync(post.PostId, cancellationToken);
    }

    private async Task<PostEntity> FindTrackedAsync(Guid postId, CancellationToken cancellationToken)
        => await _unitOfWork.Posts.GetByIdAsync(postId, cancellationToken) is { IsDeleted: false } post
            ? post
            : throw new NotFoundException("Không tìm thấy bài viết.");

    // Ghi cùng SaveChanges với bài viết nên log và dữ liệu luôn khớp
    private Task AuditAsync(string action, Guid postId, object details, DateTime now, CancellationToken cancellationToken)
        => _unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = _currentUser.RequiredUserId,
            Action = action,
            EntityType = "Post",
            EntityId = postId,
            Details = JsonSerializer.Serialize(details, AuditJson),
            CreatedAt = now
        }, cancellationToken);

    private bool IsAdmin => _currentUser.Role == RoleNames.Admin;

    private void EnsureCanEdit(PostEntity post)
    {
        if (!IsAdmin && post.CreatedByUserId != _currentUser.RequiredUserId)
            throw new ForbiddenException("Bạn chỉ được sửa bài viết do mình tạo.", ErrorCodes.Forbidden);
    }

    private static PostFilter ToFilter(PostQueryRequest query, bool publishedOnly) => new()
    {
        PublishedOnly = publishedOnly,
        PostType = query.PostType,
        TagSlug = string.IsNullOrWhiteSpace(query.Tag) ? null : query.Tag.Trim().ToLowerInvariant(),
        SoftSkillId = query.SoftSkillId,
        Search = query.Search,
        PageNumber = query.PageNumber,
        PageSize = query.PageSize
    };

    private static PagedResponse<T> ToPaged<T>(PostQueryRequest query, int total, IReadOnlyList<T> items) => new()
    {
        Items = items,
        PageNumber = query.PageNumber,
        PageSize = query.PageSize,
        TotalItems = total
    };

    private static PostListItemResponse ToListItem(PostListRow p) => new()
    {
        PostId = p.PostId,
        Title = p.Title,
        Slug = p.Slug,
        PostType = p.PostType,
        Excerpt = MakeExcerpt(p.ContentPreview),
        CoverImageUrl = MarkdownHelper.FirstImageUrl(p.ImageSnippet),
        PublishedAt = p.PublishedAt,
        AuthorName = p.AuthorName,
        Tags = p.Tags.Select(t => new PostTagResponse(t.Name, t.Slug)).ToList(),
        SoftSkills = p.SoftSkills.Select(s => new PostSoftSkillResponse(s.SoftSkillId, s.Name)).ToList()
    };

    private static IEnumerable<Tag> ActiveTags(PostEntity p)
        => p.PostTags.Select(pt => pt.Tag).Where(t => !t.IsDeleted).OrderBy(t => t.Name);

    /// <summary>Tour gắn bài chưa xoá; publishedOnly cho phía khách để không lộ tour Draft/Archived/Expired.</summary>
    private static List<PostTourResponse> ActiveTours(PostEntity p, bool publishedOnly = false)
        => p.TourPosts
            .Where(tp => !tp.Tour.IsDeleted)
            .Where(tp => !publishedOnly || tp.Tour.Status is TourStatuses.Active or TourStatuses.Published)
            .OrderBy(tp => tp.DisplayOrder)
            .Select(tp => new PostTourResponse(tp.TourId, tp.Tour.Name, tp.Tour.DurationDays, tp.Tour.DurationNights, tp.Tour.BasePrice))
            .ToList();

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
        if (await _unitOfWork.Posts.SlugExistsAsync(slug, post.PostId, cancellationToken))
            throw new ConflictException("Đường dẫn đã được dùng bởi bài khác.", errors: new Dictionary<string, string[]>
            {
                ["slug"] = ["Đường dẫn đã được dùng bởi bài khác. Thêm hậu tố, ví dụ “-2”."]
            });

        post.Title = request.Title.Trim();
        post.Slug = slug;
        post.PostType = request.PostType;
        post.Content = request.Content.Trim();

        await SyncTagsAsync(post, request.TagIds, now, cancellationToken);
        await SyncSoftSkillsAsync(post, request.SoftSkillIds, now, cancellationToken);
        await SyncToursAsync(post, request.TourIds, now, cancellationToken);
    }

    private async Task SyncTagsAsync(PostEntity post, List<Guid> tagIds, DateTime now, CancellationToken cancellationToken)
    {
        var wantedIds = tagIds.Distinct().ToList();
        if (await _unitOfWork.Tags.CountActiveAsync(wantedIds, cancellationToken) != wantedIds.Count)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["tagIds"] = ["Có tag không tồn tại hoặc đã bị xóa."]
            });

        foreach (var link in post.PostTags.Where(pt => !wantedIds.Contains(pt.TagId)).ToList())
        {
            post.PostTags.Remove(link);
            _unitOfWork.PostTags.Remove(link);
        }

        foreach (var id in wantedIds.Where(id => post.PostTags.All(pt => pt.TagId != id)))
            post.PostTags.Add(new PostTag { PostTagId = Guid.NewGuid(), PostId = post.PostId, TagId = id, CreatedAt = now });
    }

    private async Task SyncSoftSkillsAsync(PostEntity post, List<Guid> softSkillIds, DateTime now, CancellationToken cancellationToken)
    {
        var wantedIds = softSkillIds.Distinct().ToList();
        if (await _unitOfWork.SoftSkills.CountActiveAsync(wantedIds, cancellationToken) != wantedIds.Count)
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
        if (await _unitOfWork.Tours.CountByIdsAsync(wantedIds, cancellationToken) != wantedIds.Count)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["tourIds"] = ["Có tour không tồn tại hoặc đã bị xóa."]
            });

        foreach (var link in post.TourPosts.Where(tp => !wantedIds.Contains(tp.TourId)).ToList())
        {
            post.TourPosts.Remove(link);
            _unitOfWork.TourPosts.Remove(link);
        }

        // DisplayOrder là thứ tự đọc bài trong tour (admin đặt ở §6.23) nên link cũ giữ nguyên thứ tự
        for (var order = 0; order < wantedIds.Count; order++)
        {
            if (post.TourPosts.All(tp => tp.TourId != wantedIds[order]))
                post.TourPosts.Add(new TourPost { TourPostId = Guid.NewGuid(), PostId = post.PostId, TourId = wantedIds[order], DisplayOrder = order, CreatedAt = now });
        }
    }
}
