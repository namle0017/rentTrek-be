namespace RenTrek.BLL.Constants;

/// <summary>
/// Giá trị booking_participants.status.
/// </summary>
public static class BookingParticipantStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    /// <summary>Vắng ở hoạt động đầu tiên của chuyến; không hoàn tiền.</summary>
    public const string NoShow = "NoShow";
}
