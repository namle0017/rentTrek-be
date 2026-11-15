namespace RenTrek.BLL.DTOs.Post.Responses;

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

    /// <summary>Ảnh đầu tiên trong nội dung bài; null nếu bài không có ảnh.</summary>
    public string? CoverImageUrl { get; set; }

    public DateTime? PublishedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public IReadOnlyList<PostTagResponse> Tags { get; set; } = [];
    public IReadOnlyList<PostSoftSkillResponse> SoftSkills { get; set; } = [];
    public IReadOnlyList<PostTourResponse> RelatedTours { get; set; } = [];
    public IReadOnlyList<PostListItemResponse> SameTagPosts { get; set; } = [];
}
