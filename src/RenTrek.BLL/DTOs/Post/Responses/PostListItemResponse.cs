namespace RenTrek.BLL.DTOs.Post.Responses;

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
