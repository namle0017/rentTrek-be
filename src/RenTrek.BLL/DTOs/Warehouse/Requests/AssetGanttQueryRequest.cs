namespace RenTrek.BLL.DTOs.Warehouse.Requests;

public class AssetGanttQueryRequest
{
    public DateTime From { get; set; } = DateTime.UtcNow.Date;
    public DateTime To { get; set; } = DateTime.UtcNow.Date.AddDays(14);
    public Guid? CategoryId { get; set; }
    public Guid? ProductVariantId { get; set; }
}
