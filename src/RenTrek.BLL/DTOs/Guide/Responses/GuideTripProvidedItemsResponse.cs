namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Danh sách đồ cấp giao cho nhóm của Guide tại màn G06 (24.6).
/// </summary>
public sealed class GuideTripProvidedItemsResponse
{
    public Guid TripId { get; set; }
    public IReadOnlyList<GuideProvidedItemResponse> Items { get; set; } = [];
}
