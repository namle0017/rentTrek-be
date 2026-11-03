namespace RenTrek.BLL.DTOs.Tour.Responses;

public class TourRelatedPostResponse
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}

