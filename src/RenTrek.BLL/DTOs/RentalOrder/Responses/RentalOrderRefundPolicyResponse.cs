namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Chính sách hoàn tiền đã được chốt cho đơn thuê.
/// </summary>
public class RentalOrderRefundPolicyResponse
{
    public Guid RefundPolicyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; }
}
