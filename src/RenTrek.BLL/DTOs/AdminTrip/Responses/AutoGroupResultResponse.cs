namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Kết quả tự động chia nhóm theo thuật toán giữ đơn đi chung (9.6).
/// </summary>
public sealed class AutoGroupResultResponse
{
    public int AssignedCount { get; set; }
    public int RemainingUnassignedCount { get; set; }
    public KanbanGroupsBoardResponse Board { get; set; } = new();
}
