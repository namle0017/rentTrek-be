namespace RenTrek.BLL.DTOs.AdminCatalog.Requests;

/// <summary>Body thêm / sửa định mức đồ cấp (§6.19, §6.20).</summary>
public sealed class SaveTourProvidedItemRequest
{
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public string QuantityBasis { get; set; } = "PerPerson";
    public string? Note { get; set; }
}
