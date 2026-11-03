using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào khi mở link xem thông tin và lịch hoạt động của chuyến đi.
/// </summary>
public sealed class InvitationPreviewRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    [RegularExpression("^[0-9A-F]{64}$", ErrorMessage = "Link chuyến đi không hợp lệ")]
    public string Token { get; set; } = string.Empty;
}

