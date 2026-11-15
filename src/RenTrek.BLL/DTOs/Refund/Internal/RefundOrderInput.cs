namespace RenTrek.BLL.DTOs.Refund.Internal;

/// <summary>Phần dữ liệu Order mà luồng hoàn tiền cần, để service hoàn tiền không nhận entity.</summary>
public sealed record RefundOrderInput(
    Guid OrderId,
    string OrderCode,
    string OrderType,
    Guid CustomerUserId,
    Guid RefundPolicyId,
    string PaymentStatus,
    DateTimeOffset TripStartAt,
    int ActiveParticipants = 0,
    int TotalParticipants = 0,
    decimal AmountCap = 0)
{
    public static RefundOrderInput From(RenTrek.DAL.Entities.Order order) => new(
        order.OrderId,
        order.OrderCode,
        order.OrderType,
        order.CustomerUserId,
        order.RefundPolicyId,
        order.PaymentStatus,
        order.TripBookingOrder.Trip.StartAt,
        order.TripBookingOrder.Participants.Count(p => !p.IsDeleted && p.Status != BLL.Constants.BookingParticipantStatuses.Cancelled),
        order.TripBookingOrder.Participants.Count(p => !p.IsDeleted),
        order.TotalAmount);
}
