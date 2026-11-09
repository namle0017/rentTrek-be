using RenTrek.DAL.Entities.Enums;

namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng danh sách sản phẩm cho vận hành; chỉ đếm biến thể và thiết bị chưa xoá.
/// </summary>
public class AdminProductRow
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public int VariantsCount { get; set; }
    public int TotalAssetsCount { get; set; }
    public int AvailableAssetsCount { get; set; }
    public CatalogStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
