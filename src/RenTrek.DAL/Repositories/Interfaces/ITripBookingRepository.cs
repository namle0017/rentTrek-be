using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITripBookingRepository
{
    Task AcquireIdempotencyLockAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task AcquireBookingLockAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Account?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<int> GetReservedParticipantCountAsync(Guid tripId, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdempotencyKeyAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Order?> GetForAccessAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<Order?> GetOwnedBookingAsync(Guid orderId, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<decimal> GetSucceededPaymentTotalAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantInvitation>> GetOutstandingInvitationsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetBookingWithInvitationsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task AddOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task AddInvitationAsync(ParticipantInvitation invitation, CancellationToken cancellationToken = default);
}
