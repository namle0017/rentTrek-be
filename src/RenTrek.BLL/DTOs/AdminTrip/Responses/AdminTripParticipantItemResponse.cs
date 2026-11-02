namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Thông tin một hành khách trong đơn đặt chỗ tại Tab Booking O11 (8.11).
/// </summary>
public sealed class AdminTripParticipantItemResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? TripGroupId { get; set; }
    public string? TripGroupName { get; set; }
}
