namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách đơn trang quản trị.
/// </summary>
public class AdminOrderFilter
{
    /// <summary>null = mọi đơn (admin); ngược lại chỉ đơn có chuyến do người này quản lý hoặc đơn thuê họ phụ trách.</summary>
    public Guid? ScopeUserId { get; set; }

    public string? OrderType { get; set; }
    public string? Status { get; set; }
    public string? PaymentStatus { get; set; }

    /// <summary>PendingPayment | Cancelled | NeedsReconciliation | PendingRefund; giá trị khác bỏ qua.</summary>
    public string? Tab { get; set; }

    /// <summary>Tìm theo mã đơn hoặc email khách (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>CreatedAt | OrderCode | TotalAmount | Status | HoldExpiresAt | ReconciliationDeadlineAt (mặc định CreatedAt).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
