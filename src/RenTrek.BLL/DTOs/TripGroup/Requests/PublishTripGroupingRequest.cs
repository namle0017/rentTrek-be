namespace RenTrek.BLL.DTOs.TripGroup.Requests;

/// <summary>
/// Yêu cầu chốt (Publish) toàn bộ TripGroup của một Trip.
/// </summary>
public sealed class PublishTripGroupingRequest
{
    public int? ExpectedTripRowVersion { get; set; }
}
