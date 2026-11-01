namespace RenTrek.BLL.DTOs.RentalOrder.Responses;

/// <summary>
/// Chính sách hoàn tiền thuê đang hiệu lực kèm các mốc hoàn tiền.
/// </summary>
public class RentalQuoteRefundPolicyResponse
{
    public Guid RefundPolicyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; }

    public string? Description { get; set; }

    /// <summary>Các mốc hoàn tiền, giảm dần theo MinHoursBefore.</summary>
    public List<RentalQuoteRefundRuleResponse> Rules { get; set; } = [];
}
