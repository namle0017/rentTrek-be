namespace RenTrek.DAL.Models;

/// <summary>
/// Phân trang lịch sử sử dụng của một khuyến mãi (mọi bậc).
/// </summary>
public class PromotionUsageFilter
{
    public Guid PromotionId { get; set; }

    /// <summary>Chỉ hỗ trợ AppliedAt.</summary>
    public string? SortBy { get; set; }

    public bool IsDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
