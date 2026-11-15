using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITripRescheduleRepository
{
    Task AddAsync(TripBookingReschedule reschedule, CancellationToken cancellationToken = default);

    /// <summary>Các lần đổi lịch còn PendingPayment của đơn (có tracking).</summary>
    Task<IReadOnlyList<TripBookingReschedule>> GetPendingByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Lần đổi lịch gắn với khoản thanh toán chênh lệch (có tracking).</summary>
    Task<TripBookingReschedule?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>Chuyến khác cùng tour đang mở bán, khởi hành sau thời điểm cho trước, sớm nhất trước.</summary>
    Task<IReadOnlyList<Trip>> GetOpenSiblingTripsAsync(Guid tourId, Guid excludeTripId, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
