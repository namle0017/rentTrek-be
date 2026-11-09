namespace RenTrek.DAL.Models;

/// <summary>
/// Một khuyến mãi trong danh sách / chi tiết trang quản trị, kèm số đơn đã dùng.
/// </summary>
public class AdminPromotionRow
{
    public Guid PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PromotionCode { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string AppliesTo { get; set; } = string.Empty;
    public string ApplicationScope { get; set; } = string.Empty;

    /// <summary>Số đơn (distinct) có khuyến mãi ở trạng thái Applied hoặc Reserved.</summary>
    public int UsedCount { get; set; }

    public int? MaxUses { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public bool IsStackable { get; set; }
    public int Priority { get; set; }
    public string Status { get; set; } = string.Empty;
}
