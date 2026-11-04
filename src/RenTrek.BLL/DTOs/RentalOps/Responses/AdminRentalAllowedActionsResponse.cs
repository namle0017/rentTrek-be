namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalAllowedActionsResponse
{
    public bool CanHandover { get; set; }
    public string? HandoverBlockedReason { get; set; }
    public bool CanReturn { get; set; }
    public bool CanAddCharge { get; set; }
    public bool CanComplete { get; set; }
    public string? CompleteBlockedReason { get; set; }
}
