namespace RenTrek.BLL.Constants;

/// <summary>rental_evidence_photos.stage.</summary>
public static class RentalEvidenceStages
{
    public const string Handover = "Handover";
    public const string Return = "Return";
    public const string Damage = "Damage";
    public const string CustomerIssue = "CustomerIssue";
    public static readonly IReadOnlyList<string> StaffStages = [Handover, Return, Damage];
}
