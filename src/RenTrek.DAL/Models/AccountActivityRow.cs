namespace RenTrek.DAL.Models;

/// <summary>
/// Số liệu hoạt động của một tài khoản cho trang chi tiết quản trị.
/// </summary>
public class AccountActivityRow
{
    /// <summary>Số đơn tài khoản đã đặt (chưa xoá).</summary>
    public int OrderCount { get; set; }

    /// <summary>Số chuyến (chưa xoá) tài khoản quản lý.</summary>
    public int ManagedTripCount { get; set; }

    /// <summary>Số chuyến (distinct) tài khoản dẫn qua nhóm chưa xoá.</summary>
    public int GuidedTripCount { get; set; }
}
