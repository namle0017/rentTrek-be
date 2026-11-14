namespace RenTrek.DAL.Entities;

/// <summary>
/// Một bậc phạt trễ (nhận hoặc trả) tính theo % tiền cọc; bậc terminal = mất toàn bộ cọc.
/// </summary>
public class RentalPolicyTier
{
    public Guid RentalPolicyTierId { get; set; }
    public Guid RentalPolicyId { get; set; }
    public string Scope { get; set; } = null!;
    public int MinDaysLate { get; set; }
    public decimal PercentOfDeposit { get; set; }
    public bool IsTerminal { get; set; }
    public DateTime CreatedAt { get; set; }

    public RentalPolicy RentalPolicy { get; set; } = null!;
}
