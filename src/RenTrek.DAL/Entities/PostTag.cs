namespace RenTrek.DAL.Entities;

public class PostTag
{
    public Guid PostTagId { get; set; }
    public Guid PostId { get; set; }
    public Guid TagId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Post Post { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
