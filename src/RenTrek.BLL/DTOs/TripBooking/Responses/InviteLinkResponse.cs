namespace RenTrek.BLL.DTOs.TripBooking.Responses;

/// <summary>
/// Link chia sẻ để người đi cùng xem danh sách nhóm và lịch hoạt động của chuyến.
/// </summary>
public sealed class InviteLinkResponse
{
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public int TotalSeats { get; set; }
}

