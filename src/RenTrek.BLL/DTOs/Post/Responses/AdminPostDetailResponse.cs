namespace RenTrek.BLL.DTOs.Post.Responses;

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
    public IReadOnlyList<Guid> TagIds { get; set; } = [];
    public IReadOnlyList<Guid> SoftSkillIds { get; set; } = [];
    public IReadOnlyList<PostTourResponse> Tours { get; set; } = [];
    public bool CanEdit { get; set; }
}
