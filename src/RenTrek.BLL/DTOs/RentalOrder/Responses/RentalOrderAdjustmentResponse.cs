namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Một dòng lịch sử điều chỉnh đơn thuê (hiện chỉ có gia hạn).
/// </summary>
public sealed class RentalOrderAdjustmentResponse
{
    public Guid Id { get; set; }

    public string AdjustmentType { get; set; } = string.Empty;

    public DateTime OldEndAt { get; set; }

    public DateTime NewEndAt { get; set; }

    public decimal ExtraAmount { get; set; }

    public Guid RequestedByUserId { get; set; }

    public string RequestedByName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
