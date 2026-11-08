namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thay toàn bộ danh sách bài viết gắn tour (§6.23).</summary>
public sealed class TourPostsRequest
{
    public List<TourPostItemRequest> Items { get; set; } = [];
}
