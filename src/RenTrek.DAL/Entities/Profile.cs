namespace RenTrek.DAL.Entities;

public class Profile
{
    public Guid ProfileId { get; set; }
    public Guid UserId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Bio { get; set; }
    public DateOnly? CareerStartDate { get; set; }
    public string UserCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Account User { get; set; } = null!;
}
