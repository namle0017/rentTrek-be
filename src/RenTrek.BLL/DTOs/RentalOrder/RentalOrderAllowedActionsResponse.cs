namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Các thao tác khách được phép làm với đơn thuê (do server quyết định).
/// </summary>
public class RentalOrderAllowedActionsResponse
{
    public bool CanPay { get; set; }

    public bool CanCancel { get; set; }

    public string? CancelBlockedReason { get; set; }
}
