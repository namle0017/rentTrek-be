using RenTrek.BLL.DTOs.Refund.Requests;

namespace RenTrek.BLL.DTOs.RentalOrder.Requests;

/// <summary>Staff báo khách không đến nhận đồ.</summary>
public sealed class ReportRentalNoShowRequest
{
    public string? Note { get; set; }
}
