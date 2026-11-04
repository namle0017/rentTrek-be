namespace RenTrek.BLL.DTOs.Post.Responses;

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
