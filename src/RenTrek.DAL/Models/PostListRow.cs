using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng danh sách bài viết. Tags và SoftSkills chỉ gồm bản ghi chưa xoá, sắp theo tên.
/// </summary>
public class PostListRow
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Đầu nội dung, cắt thô ở DB (tối đa <see cref="PreviewLength"/> ký tự) để không kéo cả bài dài.</summary>
    public string ContentPreview { get; set; } = string.Empty;

    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public List<Tag> Tags { get; set; } = [];
    public List<SoftSkill> SoftSkills { get; set; } = [];

    /// <summary>Số tour chưa xoá gắn bài.</summary>
    public int TourCount { get; set; }

    /// <summary>Số lượt người tham gia đã đọc.</summary>
    public int ReadCount { get; set; }

    public const int PreviewLength = 440;
}
