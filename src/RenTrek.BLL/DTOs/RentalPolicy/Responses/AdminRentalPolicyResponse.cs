namespace RenTrek.BLL.DTOs.RentalPolicy.Responses;

/// <summary>
/// Chính sách thuê đồ cho màn quản trị: thêm số đơn đã snapshot version này, người tạo và thời điểm tạo.
/// Không trả ở endpoint công khai /rental-policies/active.
/// </summary>
public sealed class AdminRentalPolicyResponse : RentalPolicyResponse
{
    /// <summary>Số đơn thuê đã snapshot đúng version này lúc đặt.</summary>
    public int OrdersCount { get; set; }

    public string? CreatedByName { get; set; }

    public DateTime CreatedAt { get; set; }
}
