using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

public sealed class TripBookingRepository(AppDbContext context) : ITripBookingRepository
{
    public Task AcquireIdempotencyLockAsync(
        Guid customerUserId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => AcquireTransactionLockAsync($"trip-booking:idempotency:{customerUserId:N}:{idempotencyKey}", cancellationToken);

    public Task AcquireBookingLockAsync(Guid orderId, CancellationToken cancellationToken = default)
        => AcquireTransactionLockAsync($"trip-booking:order:{orderId:N}", cancellationToken);

    public Task<Account?> GetAccountAsync(Guid userId, CancellationToken cancellationToken = default)
        => context.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default)
        => context.Trips.FromSqlInterpolated($"SELECT * FROM trips WHERE trip_id = {tripId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> GetReservedParticipantCountAsync(
        Guid tripId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
        => await context.TripBookingOrders
            .Where(x => x.TripId == tripId &&
                (x.Order.Status == "Confirmed" ||
                 (x.Order.Status == "PendingPayment" && x.Order.HoldExpiresAt > utcNow)))
            .SumAsync(x => (int?)x.ParticipantCount, cancellationToken) ?? 0;

    public Task<Order?> GetByIdempotencyKeyAsync(
        Guid customerUserId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => BookingGraph().AsNoTracking().SingleOrDefaultAsync(
            x => x.CustomerUserId == customerUserId && x.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<Order?> GetForAccessAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
        => BookingGraph().AsNoTracking().SingleOrDefaultAsync(
            x => x.OrderId == orderId &&
                 (x.CustomerUserId == userId || x.TripBookingOrder.Participants.Any(p => p.LinkedUserId == userId)),
            cancellationToken);

    public Task<Order?> GetOwnedBookingAsync(Guid orderId, Guid ownerUserId, CancellationToken cancellationToken = default)
        => BookingGraph().SingleOrDefaultAsync(
            x => x.OrderId == orderId && x.CustomerUserId == ownerUserId,
            cancellationToken);

    public async Task<decimal> GetSucceededPaymentTotalAsync(Guid orderId, CancellationToken cancellationToken = default)
        => await context.Payments
            .Where(x => x.OrderId == orderId && x.Status == "Succeeded" && x.PaymentType != "Refund")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public async Task<IReadOnlyList<ParticipantInvitation>> GetOutstandingInvitationsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
        => await context.ParticipantInvitations
            .Where(x => x.BookingOrderId == orderId && x.RedeemedAt == null && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

    public Task<Guid?> GetInvitationBookingOrderIdAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
        => context.ParticipantInvitations
            .AsNoTracking()
            .Where(x => x.TokenHash == tokenHash)
            .Select(x => (Guid?)x.BookingOrderId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ParticipantInvitation?> GetInvitationByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
        => context.ParticipantInvitations
            .FromSqlInterpolated($"SELECT * FROM participant_invitations WHERE token_hash = {tokenHash} FOR UPDATE")
            .Include(x => x.Participant)
            .Include(x => x.BookingOrder).ThenInclude(x => x.Order)
            .Include(x => x.BookingOrder).ThenInclude(x => x.Trip)
            .Include(x => x.BookingOrder).ThenInclude(x => x.Participants)
            .SingleOrDefaultAsync(cancellationToken);

    public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default)
        => context.Orders.AddAsync(order, cancellationToken).AsTask();

    public Task AddInvitationAsync(ParticipantInvitation invitation, CancellationToken cancellationToken = default)
        => context.ParticipantInvitations.AddAsync(invitation, cancellationToken).AsTask();

    private IQueryable<Order> BookingGraph()
        => context.Orders
            .Include(x => x.TripBookingOrder).ThenInclude(x => x.Trip)
            .Include(x => x.TripBookingOrder).ThenInclude(x => x.Participants);

    private async Task AcquireTransactionLockAsync(string lockKey, CancellationToken cancellationToken)
    {
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("PostgreSQL advisory lock requires an active transaction");
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@lock_key, 0));";
        command.Parameters.AddWithValue("lock_key", lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
