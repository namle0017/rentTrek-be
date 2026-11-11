namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc và phân trang danh sách bài viết (chỉ bài chưa xoá).
/// PublishedOnly = true: chỉ bài Published, mới xuất bản trước; ngược lại lọc theo Status, mới sửa trước.
/// </summary>
public class PostFilter
{
    public bool PublishedOnly { get; set; }

    /// <summary>Bỏ qua khi PublishedOnly = true.</summary>
    public string? Status { get; set; }

    public string? PostType { get; set; }

    /// <summary>Slug của tag (đã chuẩn hoá chữ thường).</summary>
    public string? TagSlug { get; set; }

    public Guid? SoftSkillId { get; set; }

    /// <summary>Tìm theo tiêu đề (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
