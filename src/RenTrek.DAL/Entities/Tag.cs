namespace RenTrek.DAL.Entities;

public class Tag
{
    public Guid TagId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
}
