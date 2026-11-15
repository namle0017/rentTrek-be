namespace RenTrek.BLL.DTOs.RentalPolicy.Responses;

/// <summary>Một bậc phạt trễ hiển thị cho khách / admin.</summary>
public class RentalPolicyTierResponse
{
    /// <summary>PickupLate | ReturnLate.</summary>
    public string Scope { get; set; } = string.Empty;
    public int MinDaysLate { get; set; }
    public decimal PercentOfDeposit { get; set; }
    public bool IsTerminal { get; set; }
}
