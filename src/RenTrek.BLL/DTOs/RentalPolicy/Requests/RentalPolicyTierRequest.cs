namespace RenTrek.BLL.DTOs.RentalPolicy.Requests;

public class RentalPolicyTierRequest
{
    public string Scope { get; set; } = string.Empty;
    public int MinDaysLate { get; set; }
    public decimal PercentOfDeposit { get; set; }
    public bool IsTerminal { get; set; }
}
