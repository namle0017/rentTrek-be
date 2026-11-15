namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị refund_policies.status (lưu DB) và trạng thái hiển thị do BE tính.
/// </summary>
public static class RefundPolicyStatuses
{
    // Lưu trong DB
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Archived = "Archived";

    // Chỉ dùng để hiển thị / lọc (tính từ Status + khoảng hiệu lực)
    public const string Scheduled = "Scheduled";
    public const string Expired = "Expired";

    /// <summary>Năm trạng thái hiển thị hợp lệ khi lọc danh sách.</summary>
    public static readonly string[] DisplayStatuses = [Draft, Scheduled, Active, Expired, Archived];

    /// <summary>Trạng thái hiển thị theo trạng thái lưu và khoảng hiệu lực tại <paramref name="nowUtc"/>.</summary>
    public static string ToDisplay(string status, DateTime effectiveFrom, DateTime? effectiveUntil, DateTime nowUtc)
    {
        if (status == Draft) return Draft;
        if (status == Archived) return Archived;
        if (effectiveFrom > nowUtc) return Scheduled;
        if (effectiveUntil.HasValue && effectiveUntil.Value <= nowUtc) return Expired;
        return Active;
    }
}
