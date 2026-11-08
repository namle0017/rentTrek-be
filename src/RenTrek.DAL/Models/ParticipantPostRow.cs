namespace RenTrek.DAL.Models;

/// <summary>
/// Một bài viết đã xuất bản thuộc tour của hành khách kèm thời điểm đã đọc (nếu có).
/// </summary>
public class ParticipantPostRow
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime? ReadAt { get; set; }
}
