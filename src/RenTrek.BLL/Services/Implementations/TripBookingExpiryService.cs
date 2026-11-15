using RenTrek.BLL.Constants;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

public sealed class TripBookingExpiryService(IUnitOfWork unitOfWork, TimeProvider timeProvider) : ITripBookingExpiryService
{
    private const int BatchSize = 100;

    public Task<IReadOnlyList<Guid>> CollectExpiredAsync(CancellationToken cancellationToken = default)
        => unitOfWork.TripBookings.GetExpiredUnpaidHoldOrderIdsAsync(timeProvider.GetUtcNow(), BatchSize, cancellationToken);

    public async Task<bool> ExpireAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Cùng thứ tự khoá với webhook SePay và huỷ booking để không deadlock
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.TripBookings.AcquireBookingLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.TripBookings.GetBookingAsync(orderId, cancellationToken);

            // Kiểm tra lại sau khi khoá: webhook có thể vừa xác nhận đơn hoặc đưa đơn sang NeedsReconciliation
            if (order?.TripBookingOrder is null
                || order.Status != OrderStatuses.PendingPayment
                || order.PaymentStatus != OrderPaymentStatuses.Unpaid
                || order.HoldExpiresAt is null
                || order.HoldExpiresAt >= utcNow)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return false;
            }

            var pendingCollection = await unitOfWork.Payments.GetLatestPendingCollectionAsync(orderId, cancellationToken);
            if (pendingCollection is not null)
            {
                pendingCollection.Status = PaymentStatuses.Cancelled;
                pendingCollection.ProcessedAt = utcNow;
            }

            foreach (var participant in order.TripBookingOrder.Participants.Where(p => !p.IsDeleted))
            {
                participant.Status = BookingParticipantStatuses.Cancelled;
                participant.UpdatedAt = utcNow;
            }

            var promotions = await unitOfWork.Orders.GetAppliedPromotionsAsync(orderId, cancellationToken);
            foreach (var promotion in promotions.Where(x => x.Status is AppliedPromotionStatuses.Reserved or AppliedPromotionStatuses.Applied))
                promotion.Status = AppliedPromotionStatuses.Reversed;

            var invitations = await unitOfWork.Orders.GetOpenInvitationsForUpdateAsync(orderId, cancellationToken);
            foreach (var invitation in invitations)
                invitation.RevokedAt = utcNow;

            order.Status = OrderStatuses.Expired;
            order.UpdatedAt = utcNow;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
