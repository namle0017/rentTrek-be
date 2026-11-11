namespace RenTrek.DAL.Entities;

public class TourPost
{
    public Guid TourPostId { get; set; }
    public Guid TourId { get; set; }
    public Guid PostId { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Post Post { get; set; } = null!;
    public Tour Tour { get; set; } = null!;
}
