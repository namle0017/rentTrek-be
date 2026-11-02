namespace RenTrek.BLL.DTOs.Guide.Responses;

/// <summary>
/// Thông tin thiết bị cấp phát của đoàn tại màn G03 (24.3).
/// </summary>
public sealed class GuideAssignedAssetItemResponse
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}
