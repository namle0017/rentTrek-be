using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ claim participant invitation.
/// </summary>
public sealed class ClaimParticipantInvitationRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
