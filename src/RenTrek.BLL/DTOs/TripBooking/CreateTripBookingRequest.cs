using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking;

public sealed class CreateTripBookingRequest
{
    [Required]
    public Guid TripId { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [MaxLength(20)]
    public IReadOnlyList<CreateBookingParticipantRequest> AdditionalParticipants { get; set; } = [];
}
