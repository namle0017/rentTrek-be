namespace RenTrek.BLL.DTOs.Refund.Requests;

/// <summary>
/// Bộ lọc hàng đợi hồ sơ hoàn tiền của Staff/Admin.
/// </summary>
public sealed class RefundCaseQueryRequest
{
    public string? Status { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
