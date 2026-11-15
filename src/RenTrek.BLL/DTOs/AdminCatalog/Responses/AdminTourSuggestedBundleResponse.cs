namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

/// <summary>Combo gợi ý của tour (màn admin).</summary>
public sealed class AdminTourSuggestedBundleResponse
{
    public Guid BundleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Note { get; set; }
}
