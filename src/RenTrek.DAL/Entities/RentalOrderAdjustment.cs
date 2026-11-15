namespace RenTrek.DAL.Entities;

/// <summary>
/// Một lần điều chỉnh đơn thuê (hiện chỉ có gia hạn ngày trả). Bảng chỉ thêm dòng, không sửa, không xoá để giữ lịch sử đối soát.
/// </summary>
public class RentalOrderAdjustment
{
    public Guid RentalOrderAdjustmentId { get; set; }
    public Guid RentalOrderId { get; set; }
    public string AdjustmentType { get; set; } = null!;
    public DateTime OldEndAt { get; set; }
    public DateTime NewEndAt { get; set; }
    public decimal ExtraAmount { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "Applied";
    public Guid? PaymentId { get; set; }
    public DateTime? AppliedAt { get; set; }

    public RentalOrder RentalOrder { get; set; } = null!;
    public Account RequestedByUser { get; set; } = null!;
    public Payment? Payment { get; set; }
}
