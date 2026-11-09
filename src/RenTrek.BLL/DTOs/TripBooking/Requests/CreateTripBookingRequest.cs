using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào của nghiệp vụ tạo trip booking.
/// </summary>
public sealed class CreateTripBookingRequest
{
    [Required]
    public Guid TripId { get; set; }

    [StringLength(20)]
    public string ContactPhone { get; set; } = string.Empty;

    public bool BuyerParticipates { get; set; } = true;

    [StringLength(1000)]
    public string? Note { get; set; }

    [MaxLength(20)]
    public IReadOnlyList<CreateBookingParticipantRequest> AdditionalParticipants { get; set; } = [];
}
