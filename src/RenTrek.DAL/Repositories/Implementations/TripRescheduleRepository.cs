using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Truy cập dữ liệu đổi lịch booking qua EF Core.
/// </summary>
public sealed class TripRescheduleRepository(AppDbContext context) : ITripRescheduleRepository
{
    public Task AddAsync(TripBookingReschedule reschedule, CancellationToken cancellationToken = default)
        => context.TripBookingReschedules.AddAsync(reschedule, cancellationToken).AsTask();

    public async Task<IReadOnlyList<TripBookingReschedule>> GetPendingByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
        => await context.TripBookingReschedules
            .Where(x => x.OrderId == orderId && x.Status == "PendingPayment")
            .ToListAsync(cancellationToken);

    public Task<TripBookingReschedule?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => context.TripBookingReschedules.SingleOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetOpenSiblingTripsAsync(
        Guid tourId,
        Guid excludeTripId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
        => await context.Trips
            .AsNoTracking()
            .Where(x => x.TourId == tourId && x.TripId != excludeTripId && !x.IsDeleted
                        && x.Status == "Open" && x.StartAt > utcNow)
            .OrderBy(x => x.StartAt)
            .ToListAsync(cancellationToken);
}
