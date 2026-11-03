namespace RenTrek.BLL.DTOs.Post.Requests;

/// <summary>
/// Dữ liệu tạo / cập nhật bài viết từ trình soạn O32.
/// </summary>
public sealed class UpsertPostRequest
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Bỏ trống → tự sinh từ tiêu đề.</summary>
    public string? Slug { get; set; }

    public string PostType { get; set; } = string.Empty;

    /// <summary>Văn bản thuần; dòng bắt đầu bằng "## " là tiêu đề mục.</summary>
    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>Tên tag; tag chưa có sẽ được tạo nhanh.</summary>
    public List<string> TagNames { get; set; } = [];

    public List<Guid> SoftSkillIds { get; set; } = [];

    /// <summary>Thứ tự trong danh sách = TOUR_POSTS.display_order.</summary>
    public List<Guid> TourIds { get; set; } = [];
}
