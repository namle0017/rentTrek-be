namespace RenTrek.BLL.DTOs.Participation.Responses;

/// <summary>
/// Thông tin một bài viết hướng dẫn của tour kèm trạng thái đã đọc của hành khách.
/// </summary>
public sealed class ParticipantPostResponse
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
