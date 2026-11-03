namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Cập nhật cấu hình sức chứa nhóm chuẩn cho phiên bản Tour.
/// </summary>
public sealed class UpdateTourGroupSettingsRequest
{
    public int GroupMaxParticipants { get; set; }
}
