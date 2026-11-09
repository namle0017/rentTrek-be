using System.ComponentModel.DataAnnotations;

namespace RenTrek.BLL.DTOs.RentalOrder.Requests;

/// <summary>Cancel an unpaid rental before equipment has been handed over.</summary>
public sealed class CancelRentalOrderRequest
{
    [StringLength(500)]
    public string? Reason { get; set; }
}
