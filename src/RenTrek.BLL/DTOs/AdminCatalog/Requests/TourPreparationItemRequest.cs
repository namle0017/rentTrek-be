namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Một dòng đồ khách tự mang.</summary>
public sealed class TourPreparationItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public string? Note { get; set; }
}
