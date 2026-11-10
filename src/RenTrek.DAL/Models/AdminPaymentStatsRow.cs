namespace RenTrek.DAL.Models;

/// <summary>
/// Tổng hợp giao dịch cho thẻ KPI màn O27 (cùng bộ lọc và phạm vi với danh sách giao dịch).
/// </summary>
public class AdminPaymentStatsRow
{
    /// <summary>Tổng Collection đã Succeeded.</summary>
    public decimal TotalCollectionSucceeded { get; set; }

    /// <summary>Tổng Refund đã Succeeded.</summary>
    public decimal TotalRefundSucceeded { get; set; }

    /// <summary>Số giao dịch Pending hoặc PendingApproval (mọi loại).</summary>
    public int PendingCount { get; set; }

    /// <summary>Tổng tiền của các giao dịch Pending hoặc PendingApproval.</summary>
    public decimal PendingAmount { get; set; }
}
