namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách đơn hàng của khách.
/// </summary>
public class MyOrderFilter
{
    /// <summary>orders.order_type (vd. Rental, TripBooking); null = mọi loại.</summary>
    public string? OrderType { get; set; }

    /// <summary>orders.status; null = mọi trạng thái.</summary>
    public string? Status { get; set; }

    /// <summary>Tìm theo mã đơn (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>createdAt | serviceStartAt (mặc định createdAt).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
