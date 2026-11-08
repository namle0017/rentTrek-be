namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>
/// Một dòng thay đổi. Section: Info / Activities / ProvidedItems / PreparationItems / Posts.
/// Change: Added / Removed / Modified. Before/After là chuỗi mô tả để hiển thị thẳng.
/// </summary>
public sealed class AdminTourDiffItemResponse
{
    public string Section { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
}
