namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Tiêu chí 3: Mọi nhóm đều đã có Hướng dẫn viên phụ trách (8.5).
/// </summary>
public sealed class ReadinessGroupsGuidedItemResponse
{
    public bool Passed { get; set; }
    public int AssignedGroups { get; set; }
    public int TotalGroups { get; set; }
}
