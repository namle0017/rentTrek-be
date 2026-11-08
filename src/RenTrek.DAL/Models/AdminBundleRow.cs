namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng combo cho màn quản trị O19 (kèm số thành phần chưa xoá).
/// </summary>
public class AdminBundleRow
{
    public Guid BundleId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
