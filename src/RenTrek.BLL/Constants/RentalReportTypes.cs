namespace RenTrek.BLL.Constants;

/// <summary>rental_reports.report_type.</summary>
public static class RentalReportTypes
{
    public const string NoShow = "NoShow";
    public const string LateCancellation = "LateCancellation";
    public const string ReturnInspection = "ReturnInspection";
    public const string SuspectedLoss = "SuspectedLoss";
    public const string ExtraPayment = "ExtraPayment";
    public static readonly IReadOnlyList<string> All = [NoShow, LateCancellation, ReturnInspection, SuspectedLoss, ExtraPayment];
}
