namespace RenTrek.DAL.Models;

/// <summary>
/// Tag chưa xoá kèm số bài viết chưa xoá gắn tag.
/// </summary>
public class AdminTagRow
{
    public Guid TagId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int PostCount { get; set; }
}
