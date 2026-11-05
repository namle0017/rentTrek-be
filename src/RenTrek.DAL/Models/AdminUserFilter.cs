namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách tài khoản trang quản trị (chỉ tài khoản chưa xoá).
/// </summary>
public class AdminUserFilter
{
    /// <summary>Tìm theo họ tên, email, số điện thoại hoặc mã người dùng (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>Tên vai trò.</summary>
    public string? Role { get; set; }

    public string? Status { get; set; }

    /// <summary>CreatedAt | FullName | Email | Status (mặc định CreatedAt).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
