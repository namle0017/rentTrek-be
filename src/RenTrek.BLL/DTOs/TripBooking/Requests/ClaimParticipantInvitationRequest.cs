using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào khi nhập mã nhóm: booking của link mời và mã 6 số chung của nhóm.
/// </summary>
public sealed class ClaimParticipantInvitationRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác nhận gồm đúng 6 chữ số")]
    public string Code { get; set; } = string.Empty;
}
