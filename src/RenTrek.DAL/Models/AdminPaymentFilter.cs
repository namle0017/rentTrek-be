namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách giao dịch thu / hoàn trang quản trị.
/// </summary>
public class AdminPaymentFilter
{
    /// <summary>null = mọi giao dịch (admin); ngược lại chỉ giao dịch của đơn trong phạm vi người này phụ trách.</summary>
    public Guid? ScopeUserId { get; set; }

    /// <summary>Collection | Refund.</summary>
    public string? PaymentType { get; set; }

    /// <summary>TourFee | RentalFee | AdditionalCharge.</summary>
    public string? Purpose { get; set; }

    public string? Status { get; set; }

    /// <summary>
    /// Tab hàng đợi hoàn tiền: PendingApproval | Pending | Failed lọc khoản hoàn theo trạng thái;
    /// NeedsReconciliation lọc khoản tiền đã nhận (Collection Pending có mã giao dịch) của đơn cần đối soát.
    /// </summary>
    public string? Tab { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>Tìm theo mã đơn hoặc mã giao dịch nhà cung cấp (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>CreatedAt | Amount | Status (mặc định CreatedAt).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
