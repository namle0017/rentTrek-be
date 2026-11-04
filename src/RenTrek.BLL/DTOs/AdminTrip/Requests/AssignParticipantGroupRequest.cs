namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu gán hoặc chuyển nhóm cho một hành khách trên bảng Kanban (9.5).
/// </summary>
public sealed class AssignParticipantGroupRequest
{
    public Guid ParticipantId { get; set; }
    public Guid? TargetGroupId { get; set; }
}
