namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một dòng combo trong danh sách quản trị (§13.14).</summary>
public sealed class AdminBundleListItemResponse
{
    public Guid BundleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
