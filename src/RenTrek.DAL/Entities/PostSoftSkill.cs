namespace RenTrek.DAL.Entities;

public class PostSoftSkill
{
    public Guid PostSoftSkillId { get; set; }
    public Guid PostId { get; set; }
    public Guid SoftSkillId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Post Post { get; set; } = null!;
    public SoftSkill SoftSkill { get; set; } = null!;
}
