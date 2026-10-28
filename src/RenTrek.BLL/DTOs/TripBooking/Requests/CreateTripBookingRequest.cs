using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ create trip booking.
/// </summary>
public sealed class CreateTripBookingRequest
{
    [Required]
    public Guid TripId { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [MaxLength(20)]
    public IReadOnlyList<CreateBookingParticipantRequest> AdditionalParticipants { get; set; } = [];
}
