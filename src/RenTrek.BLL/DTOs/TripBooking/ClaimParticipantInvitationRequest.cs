using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking;

public sealed class ClaimParticipantInvitationRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
