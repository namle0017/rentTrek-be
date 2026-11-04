namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class GanttDailySummaryResponse
{
    public string Date { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int InUseCount { get; set; }
    public int AvailableCount { get; set; }
    public bool IsOverCapacity { get; set; }
}
