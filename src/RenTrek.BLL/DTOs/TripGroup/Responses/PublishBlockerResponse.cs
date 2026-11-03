namespace RenTrek.BLL.DTOs.TripGroup.Responses;

/// <summary>
/// Lý do chặn chưa cho phép chốt (Publish) các TripGroup của chuyến đi.
/// </summary>
public sealed class PublishBlockerResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
