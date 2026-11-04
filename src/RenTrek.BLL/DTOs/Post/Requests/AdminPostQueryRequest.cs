using RenTrek.BLL.DTOs.Common.Requests;

namespace RenTrek.BLL.DTOs.Post.Requests;

/// <summary>
/// Bộ lọc danh sách bài viết vận hành (O32).
/// </summary>
public sealed class AdminPostQueryRequest : PostQueryRequest
{
    public string? Status { get; set; }
}
