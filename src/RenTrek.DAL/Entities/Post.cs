namespace RenTrek.DAL.Entities;

public class Post
{
    public Guid PostId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string PostType { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Account CreatedByUser { get; set; } = null!;
    public ICollection<ParticipantPostRead> ParticipantPostReads { get; set; } = new List<ParticipantPostRead>();
    public ICollection<PostSoftSkill> PostSoftSkills { get; set; } = new List<PostSoftSkill>();
    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    public ICollection<TourPost> TourPosts { get; set; } = new List<TourPost>();
}
