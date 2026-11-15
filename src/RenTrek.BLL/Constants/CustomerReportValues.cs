namespace RenTrek.BLL.Constants;

/// <summary>customer_reports.source_type.</summary>
public static class CustomerReportSourceTypes
{
    public const string Rental = "Rental";
    public const string Trip = "Trip";
    public static readonly IReadOnlyList<string> All = [Rental, Trip];
}

/// <summary>customer_reports.status.</summary>
public static class CustomerReportStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Rejected = "Rejected";
    public static readonly IReadOnlyList<string> All = [Open, InProgress, Resolved, Rejected];

    /// <summary>Đã đóng: không đổi trạng thái được nữa.</summary>
    public static bool IsClosed(string status) => status is Resolved or Rejected;
}

/// <summary>customer_reports.category theo nguồn báo cáo.</summary>
public static class CustomerReportCategories
{
    public static readonly IReadOnlyList<string> Rental =
        ["Damaged", "WrongVariant", "MissingAccessory", "NotAsDescribed", "Other"];

    public static readonly IReadOnlyList<string> Trip =
        ["Guide", "Schedule", "Safety", "Service", "Equipment", "Payment", "Other"];

    public static IReadOnlyList<string> For(string sourceType)
        => sourceType == CustomerReportSourceTypes.Rental ? Rental : Trip;
}

/// <summary>customer_reports.preferred_action (mong muốn của khách, tuỳ chọn).</summary>
public static class CustomerReportPreferredActions
{
    public static readonly IReadOnlyList<string> All = ["Exchange", "Refund", "Repair", "Feedback", "Other"];
}
