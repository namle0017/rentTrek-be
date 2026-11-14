namespace RenTrek.BLL.Constants;

/// <summary>rental_reports.status.</summary>
public static class RentalReportStatuses
{
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Resolved = "Resolved";
    public static readonly IReadOnlyList<string> All = [Submitted, Approved, Rejected, Resolved];
}
