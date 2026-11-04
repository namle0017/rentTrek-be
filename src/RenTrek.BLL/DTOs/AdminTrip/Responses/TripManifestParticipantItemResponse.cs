namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin hành khách trong danh sách xuất Manifest (8.12).
/// </summary>
public sealed class TripManifestParticipantItemResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string? GuideName { get; set; }
}
