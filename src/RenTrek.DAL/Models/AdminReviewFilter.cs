namespace RenTrek.DAL.Models;

/// <summary>
/// Bộ lọc, sắp xếp và phân trang danh sách đánh giá ở trang quản trị.
/// </summary>
public class AdminReviewFilter
{
    public int? Rating { get; set; }

    /// <summary>Tour của chuyến mà người đánh giá tham gia.</summary>
    public Guid? TourId { get; set; }

    /// <summary>Hướng dẫn viên của nhóm mà người đánh giá thuộc về.</summary>
    public Guid? GuideUserId { get; set; }

    /// <summary>true = chỉ đánh giá bị ẩn, false = chỉ đánh giá đang hiện; null = tất cả.</summary>
    public bool? IsHidden { get; set; }

    /// <summary>Tìm trong nội dung đánh giá (chứa, không phân biệt hoa thường).</summary>
    public string? Search { get; set; }

    /// <summary>CreatedAt | Rating (mặc định CreatedAt).</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
