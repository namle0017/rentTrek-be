namespace RenTrek.DAL.Models;

/// <summary>
/// Một dòng lịch sử điều chỉnh đơn thuê kèm họ tên người yêu cầu, dùng cho màn khách và màn vận hành.
/// </summary>
public class RentalOrderAdjustmentRow
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
