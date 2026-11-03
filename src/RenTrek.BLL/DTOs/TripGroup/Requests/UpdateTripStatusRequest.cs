namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Yêu cầu chuyển trạng thái Trip (ví dụ: Draft -> Open, Open -> ClosedForBooking).
/// </summary>
public sealed class UpdateTripStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
