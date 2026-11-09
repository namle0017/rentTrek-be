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

    /// <summary>Markdown. Bài mới luôn là Draft; đổi trạng thái qua publish / archive.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Tag lấy từ GET /admin/tags.</summary>
    public List<Guid> TagIds { get; set; } = [];

    public List<Guid> SoftSkillIds { get; set; } = [];

    /// <summary>Thứ tự trong danh sách = TOUR_POSTS.display_order.</summary>
    public List<Guid> TourIds { get; set; } = [];
}
