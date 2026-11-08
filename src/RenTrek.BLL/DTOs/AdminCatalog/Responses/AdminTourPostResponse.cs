namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Bài viết gắn tour theo thứ tự đọc (§6.22).</summary>
public sealed class AdminTourPostResponse
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
