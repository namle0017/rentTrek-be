namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class BulkCreateAssetsRequest
{
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public string CodePrefix { get; set; } = string.Empty;
    public string? InitialConditionNote { get; set; }
}
