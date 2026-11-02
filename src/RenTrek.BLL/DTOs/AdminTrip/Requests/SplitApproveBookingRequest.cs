namespace RenTrek.BLL.DTOs.AdminTrip.Requests;

/// <summary>
/// Yêu cầu phê duyệt tách nhóm cho một đơn đặt chỗ có nguyện vọng đi chung (9.7).
/// </summary>
public sealed class SplitApproveBookingRequest
{
    public string Reason { get; set; } = string.Empty;
}
