namespace RenTrek.BLL.DTOs.DevClock.Requests;

public sealed class SetDevClockRequest
{
    /// <summary>Thời điểm muốn hệ thống coi là "bây giờ" (ISO 8601).</summary>
    public DateTimeOffset Now { get; set; }
}
