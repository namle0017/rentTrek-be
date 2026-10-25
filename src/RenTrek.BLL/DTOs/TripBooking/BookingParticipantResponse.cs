namespace RenTrek.BLL.DTOs.TripBooking;

/// <summary>
/// Mô tả dữ liệu trả về của nghiệp vụ booking participant.
/// </summary>
public sealed class BookingParticipantResponse
{
    public Guid ParticipantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? LinkedUserId { get; set; }
    public string Status { get; set; } = string.Empty;
}
