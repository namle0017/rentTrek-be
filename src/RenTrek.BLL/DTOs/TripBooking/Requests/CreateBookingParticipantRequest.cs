using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của từng người đi cùng khi tạo trip booking.
/// </summary>
public sealed class CreateBookingParticipantRequest
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(200)]
    public string? EmergencyContactName { get; set; }

    [StringLength(20)]
    public string? EmergencyContactPhone { get; set; }
}
