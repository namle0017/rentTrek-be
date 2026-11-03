namespace RenTrek.BLL.DTOs.Post.Responses;

public sealed record PostTagResponse(string Name, string Slug);

public sealed record PostSoftSkillResponse(Guid SoftSkillId, string Name);

public sealed record PostTourResponse(Guid TourId, string Name, int DurationDays, int DurationNights, decimal BasePrice);

public sealed record PostTaxonomyResponse(
    IReadOnlyList<PostTagResponse> Tags,
    IReadOnlyList<PostSoftSkillResponse> SoftSkills);

/// <summary>
/// Thẻ bài viết công khai (C17, mục "Cùng tag" ở C18).
/// </summary>
public sealed class PostListItemResponse
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public IReadOnlyList<PostTagResponse> Tags { get; set; } = [];
    public IReadOnlyList<PostSoftSkillResponse> SoftSkills { get; set; } = [];
}

/// <summary>
/// Chi tiết bài viết công khai (C18).
/// </summary>
public sealed class PostDetailResponse
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public IReadOnlyList<PostTagResponse> Tags { get; set; } = [];
    public IReadOnlyList<PostSoftSkillResponse> SoftSkills { get; set; } = [];
    public IReadOnlyList<PostTourResponse> RelatedTours { get; set; } = [];
    public IReadOnlyList<PostListItemResponse> SameTagPosts { get; set; } = [];
}

/// <summary>
/// Dòng danh sách bài viết vận hành (O32).
/// </summary>
public sealed class AdminPostListItemResponse
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int TourCount { get; set; }
    public int ReadCount { get; set; }
    public bool CanEdit { get; set; }
}

/// <summary>
/// Dữ liệu nạp vào trình soạn O32.
/// </summary>
public sealed class AdminPostDetailResponse
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public IReadOnlyList<string> TagNames { get; set; } = [];
    public IReadOnlyList<Guid> SoftSkillIds { get; set; } = [];
    public IReadOnlyList<PostTourResponse> Tours { get; set; } = [];
    public bool CanEdit { get; set; }
}
