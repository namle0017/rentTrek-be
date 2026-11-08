namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Kết quả gia hạn thành công: dòng điều chỉnh vừa ghi và chi tiết đơn thuê sau khi áp dụng.
/// </summary>
public sealed class RentalExtensionResultResponse
{
    public RentalOrderAdjustmentResponse Adjustment { get; set; } = new();

    public RentalOrderDetailResponse Order { get; set; } = new();
}
