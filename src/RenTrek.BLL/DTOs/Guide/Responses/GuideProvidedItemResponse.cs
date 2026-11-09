namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Một hạng mục đồ cấp giao cho đoàn tại màn G06 (24.6).
/// </summary>
public sealed class GuideProvidedItemResponse
{
    public Guid ProvidedItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }
    public int AllocatedQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<GuideAllocatedAssetItemResponse> AllocatedAssets { get; set; } = [];
}
