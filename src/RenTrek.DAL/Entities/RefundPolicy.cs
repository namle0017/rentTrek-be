namespace RenTrek.DAL.Entities;

public class RefundPolicy
{
    public Guid RefundPolicyId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string PolicyType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public Guid PolicyFamilyId { get; set; }
    public int Version { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    /// <summary>Đổi lịch phải trước giờ khởi hành ít nhất N giờ; null = không cho đổi lịch.</summary>
    public int? RescheduleMinHoursBefore { get; set; }
    public decimal RescheduleFee { get; set; }
    public int MaxReschedules { get; set; }
    /// <summary>Trễ quá N phút không báo ⇒ NoShow.</summary>
    public int NoShowGraceMinutes { get; set; } = 30;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Account CreatedByUser { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<RefundPolicyRule> RefundPolicyRules { get; set; } = new List<RefundPolicyRule>();
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
