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

    /// <summary>Version chính sách hoàn tiền khách đã đọc và đồng ý; lệch version hiện hành ⇒ 409 REFUND_POLICY_CHANGED.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Phiên bản chính sách hoàn tiền là bắt buộc")]
    public int AcceptedRefundPolicyVersion { get; set; }

    [MaxLength(20)]
    public IReadOnlyList<CreateBookingParticipantRequest> AdditionalParticipants { get; set; } = [];
}
