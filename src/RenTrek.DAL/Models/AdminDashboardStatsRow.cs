namespace RenTrek.DAL.Models;

/// <summary>
/// Các bộ đếm trên dashboard quản trị (đã giới hạn theo phạm vi staff nếu có).
/// </summary>
public class AdminDashboardStatsRow
{
    public int PendingPaymentExpiringCount { get; set; }
    public int TripsDepartingIn7DaysCount { get; set; }
    public int UnassignedParticipantsCount { get; set; }
    public int MissingProvidedItemsCount { get; set; }
    public int UnderCapacityTripsCount { get; set; }
    public int OverdueRentalAssetsCount { get; set; }

    /// <summary>Chỉ tính khi không giới hạn phạm vi (admin); 0 với staff.</summary>
    public int PendingApprovalRefundsCount { get; set; }

    /// <summary>Chỉ tính khi không giới hạn phạm vi (admin); 0 với staff.</summary>
    public int FailedRefundsCount { get; set; }

    public int NeedsReconciliationOrdersCount { get; set; }
    public int DraftRentalChargesCount { get; set; }
}
