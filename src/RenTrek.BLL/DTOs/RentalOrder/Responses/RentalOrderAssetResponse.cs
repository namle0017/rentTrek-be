namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Tài sản vật lý đã giao cho khách (không trả các phân bổ mới chỉ ở trạng thái Reserved).
/// </summary>
public class RentalOrderAssetResponse
{
    public string AssetCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public Guid RentalOrderItemId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string? ReturnCondition { get; set; }
}
