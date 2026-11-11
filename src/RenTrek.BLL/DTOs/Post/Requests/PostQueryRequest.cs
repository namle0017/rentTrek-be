using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Post.Requests;

/// <summary>
/// Bộ lọc danh sách bài viết công khai (C17).
/// </summary>
public class PostQueryRequest : PagingRequest
{
    public string? PostType { get; set; }

    /// <summary>TAGS.slug</summary>
    public string? Tag { get; set; }

    public Guid? SoftSkillId { get; set; }
}
