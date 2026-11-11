namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Một bài viết gắn vào tour kèm thứ tự đọc.</summary>
public sealed class TourPostItemRequest
{
    public Guid PostId { get; set; }
    public int DisplayOrder { get; set; }
}
