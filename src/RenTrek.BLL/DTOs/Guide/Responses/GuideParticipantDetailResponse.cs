namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Hồ sơ an toàn và cứu hộ khẩn cấp của một khách hàng tại màn G03 (24.3).
/// </summary>
public sealed class GuideParticipantDetailResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; } = string.Empty;
    public GuideEmergencyContactResponse EmergencyContact { get; set; } = new();
    public IReadOnlyList<GuideAssignedAssetItemResponse> AssignedAssets { get; set; } = [];
    public IReadOnlyList<GuideParticipantAttendanceItemResponse> AttendanceHistory { get; set; } = [];
}
