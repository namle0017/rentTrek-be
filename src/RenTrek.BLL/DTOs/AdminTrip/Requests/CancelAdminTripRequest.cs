namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu hủy chuyến đi khẩn cấp (8.10).
/// </summary>
public sealed class CancelAdminTripRequest
{
    public string Reason { get; set; } = string.Empty;
}
