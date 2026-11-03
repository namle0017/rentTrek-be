using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.TripBooking.Requests;

/// <summary>
/// Mô tả dữ liệu đầu vào khi người đặt sửa thông tin một chỗ trong trip booking.
/// </summary>
public sealed class UpdateBookingParticipantRequest
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }
}

