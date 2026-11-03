namespace RenTrek.BLL.DTOs.Refund.Responses;

/// <summary>
/// Một mốc đã xảy ra của hồ sơ; <c>Status</c> dùng cùng giá trị với trạng thái hồ sơ.
/// </summary>
public sealed class RefundTimelineEventResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
