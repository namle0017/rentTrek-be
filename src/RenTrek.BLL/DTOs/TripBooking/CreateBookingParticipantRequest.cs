using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking;

public sealed class CreateBookingParticipantRequest
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(200)]
    public string? EmergencyContactName { get; set; }

    [StringLength(20)]
    public string? EmergencyContactPhone { get; set; }
}
