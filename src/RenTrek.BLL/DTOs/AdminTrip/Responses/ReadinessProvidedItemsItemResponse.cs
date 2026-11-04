namespace RenTrek.BLL.DTOs.AdminTrip.Responses;

/// <summary>
/// Tiêu chí 4: Toàn bộ thiết bị cấp phát cho đoàn đã sẵn sàng (8.5).
/// </summary>
public sealed class ReadinessProvidedItemsItemResponse
{
    public bool Passed { get; set; }
    public int MissingItemsCount { get; set; }
}
