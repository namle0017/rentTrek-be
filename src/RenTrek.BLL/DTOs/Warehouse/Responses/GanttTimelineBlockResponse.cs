namespace RenTrek.BLL.DTOs.Warehouse.Responses;

public class GanttTimelineBlockResponse
{
    public Guid AssignmentId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
}
