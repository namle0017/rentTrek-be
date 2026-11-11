namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách khuyến mãi trang quản trị (chỉ khuyến mãi chưa xoá).
/// </summary>
public class AdminPromotionFilter
{
    public string? Status { get; set; }
    public string? AppliesTo { get; set; }

    /// <summary>Tìm theo tên hoặc mã khuyến mãi (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>CreatedAt | Name | StartAt | EndAt | Priority (mặc định CreatedAt).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
