namespace RenTrek.DAL.Entities;

public class UserExternalLogin
{
    public Guid UserExternalLoginId { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = null!;
    public string ProviderSubject { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Account User { get; set; } = null!;
}
