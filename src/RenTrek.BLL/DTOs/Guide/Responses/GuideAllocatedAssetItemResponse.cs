namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Thông tin mã định danh tài sản vật lý đã cấp cho đoàn tại màn G06 (24.6).
/// </summary>
public sealed class GuideAllocatedAssetItemResponse
{
    public string AssetCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
