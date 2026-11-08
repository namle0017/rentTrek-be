namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Một dòng đồ khách tự mang (§6.16).</summary>
public sealed class AdminTourPreparationItemResponse
{
    public Guid PreparationItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Note { get; set; }
}
