namespace RenTrek.DAL.Entities;

public class RefundPolicyRule
{
    public Guid RefundPolicyRuleId { get; set; }
    public Guid RefundPolicyId { get; set; }
    public int MinHoursBefore { get; set; }
    public decimal RefundPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public RefundPolicy RefundPolicy { get; set; } = null!;
}
