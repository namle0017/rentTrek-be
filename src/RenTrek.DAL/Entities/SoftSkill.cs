namespace RenTrek.DAL.Entities;

public class SoftSkill
{
    public Guid SoftSkillId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<PostSoftSkill> PostSoftSkills { get; set; } = new List<PostSoftSkill>();
}
