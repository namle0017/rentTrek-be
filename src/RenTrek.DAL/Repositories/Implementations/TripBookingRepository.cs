using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho trip booking qua EF Core.
/// </summary>
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

    public async Task<IReadOnlyList<Account>> GetConfirmedAccountsByEmailsAsync(
        IReadOnlyCollection<string> normalizedEmails,
        CancellationToken cancellationToken = default)
    {
        if (normalizedEmails.Count == 0)
            return [];

        return await context.Accounts
            .AsNoTracking()
            .Where(a => a.EmailConfirmedAt != null
                        && a.Status.ToLower() == "active"
                        && normalizedEmails.Contains(a.Email.ToLower()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookingParticipant>> GetUnlinkedPaidParticipantsByEmailAsync(
        string normalizedEmail,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
        => await context.BookingParticipants
            .Include(p => p.BookingOrder).ThenInclude(b => b.Order)
            .Include(p => p.Trip)
            .Where(p => p.LinkedUserId == null
                        && p.Email != null
                        && p.Email.ToLower() == normalizedEmail
                        && !p.BookingOrder.Order.IsDeleted
                        && p.BookingOrder.Order.Status == "Confirmed"
                        && p.BookingOrder.Order.PaymentStatus == "Paid"
                        && p.Trip.StartAt > utcNow)
            .ToListAsync(cancellationToken);

    public Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default)
        => context.Trips.FromSqlInterpolated($"SELECT * FROM trips WHERE trip_id = {tripId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    // Booking đang chiếm chỗ: đã xác nhận, hoặc đang giữ chỗ chưa hết hạn
    internal static Expression<Func<TripBookingOrder, bool>> HoldsSeats(DateTimeOffset utcNow)
        => x => x.Order.Status == "Confirmed" ||
                (x.Order.Status == "PendingPayment" && x.Order.HoldExpiresAt > utcNow);

    public async Task<int> GetReservedParticipantCountAsync(
        Guid tripId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
        => await context.TripBookingOrders
            .Where(x => x.TripId == tripId)
            .Where(HoldsSeats(utcNow))
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
                 (x.CustomerUserId == userId ||
                  x.TripBookingOrder.Participants.Any(p =>
                      p.LinkedUserId == userId &&
                      p.LinkedUser != null &&
                      p.LinkedUser.Status.ToLower() == "active" &&
                      p.LinkedUser.EmailConfirmedAt != null)),
            cancellationToken);

    public Task<Order?> GetOwnedBookingAsync(Guid orderId, Guid ownerUserId, CancellationToken cancellationToken = default)
        => BookingGraph().SingleOrDefaultAsync(
            x => x.OrderId == orderId && x.CustomerUserId == ownerUserId,
            cancellationToken);

    public async Task<decimal> GetSucceededPaymentTotalAsync(Guid orderId, CancellationToken cancellationToken = default)
        => await context.Payments
            .Where(x => x.OrderId == orderId && x.Status == "Succeeded" && x.PaymentType != "Refund")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    // Có tracking: dùng cho cả trang xem chuyến và webhook gán tài khoản
    public Task<Order?> GetBookingAsync(Guid orderId, CancellationToken cancellationToken = default)
        => BookingGraph().SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);

    public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default)
        => context.Orders.AddAsync(order, cancellationToken).AsTask();

    private IQueryable<Order> BookingGraph()
        => context.Orders
            .AsSplitQuery()
            .Include(x => x.CustomerUser)
            .Include(x => x.TripBookingOrder).ThenInclude(x => x.Trip).ThenInclude(x => x.Tour).ThenInclude(x => x.Activities)
            .Include(x => x.TripBookingOrder).ThenInclude(x => x.Participants).ThenInclude(x => x.LinkedUser)
            .Include(x => x.TripBookingOrder).ThenInclude(x => x.Participants).ThenInclude(x => x.TripGroup).ThenInclude(x => x!.GuideUser);

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
