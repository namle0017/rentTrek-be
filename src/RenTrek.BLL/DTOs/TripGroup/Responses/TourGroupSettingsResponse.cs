namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Kết quả cập nhật cấu hình sức chứa nhóm chuẩn cho Tour.
/// </summary>
public sealed class TourGroupSettingsResponse
{
    public Guid TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public int Version { get; set; }
    public int GroupMaxParticipants { get; set; }
}
