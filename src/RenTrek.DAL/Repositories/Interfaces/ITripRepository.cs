using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

/// <summary>
/// Định nghĩa truy cập dữ liệu cho thực thể Trip và các nghiệp vụ điều hành chuyến đi.
/// </summary>
public interface ITripRepository : IGenericRepository<Trip>
{
    Task<Trip?> GetTripWithTourAndRequirementsAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Trip> Items, int TotalCount)> GetPagedAdminTripsAsync(
        Guid? tourId,
        string? status,
        DateTimeOffset? fromStartAt,
        DateTimeOffset? toStartAt,
        Guid? managedByUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, int>> GetSoldSlotsByTripIdsAsync(
        IReadOnlyCollection<Guid> tripIds,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<Trip?> GetAdminTripDetailAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<Tour?> GetTourWithProvidedItemsAsync(Guid tourId, CancellationToken cancellationToken = default);
    Task<RefundPolicy?> GetActiveRefundPolicyAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);
    Task<Account?> GetAccountByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetSoldSlotsCountAsync(Guid tripId, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBookingOrder>> GetTripBookingsWithDetailsAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripBookingOrder>> GetTripBookingsForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripGroup>> GetActiveTripGroupsAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripGroup>> GetActiveTripGroupsForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsByTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripProvidedItem>> GetTripProvidedItemsWithAssetsAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TripProvidedItem>> GetTripProvidedItemsForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetSucceededPaymentsByOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken = default);
    Task AddPaymentsAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default);
    Task AddTripProvidedItemsAsync(IEnumerable<TripProvidedItem> items, CancellationToken cancellationToken = default);
    Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken = default);
}
