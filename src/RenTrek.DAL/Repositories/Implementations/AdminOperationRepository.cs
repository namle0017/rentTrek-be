using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace RenTrek.DAL.Repositories.Implementations;

// Chuỗi trạng thái khớp RenTrek.BLL.Constants (OrderStatuses, AssignmentStatuses, PaymentStatuses...).
public sealed class AdminOperationRepository(AppDbContext context) : IAdminOperationRepository
{
    public async Task AcquireLockAsync(string key, CancellationToken cancellationToken = default)
    {
        await PostgresAdvisoryLock.AcquireAsync(context, "admin:" + key, cancellationToken);
        if (key.StartsWith("order:", StringComparison.Ordinal))
        {
            var code = key[6..];
            var orderId = await context.Orders.AsNoTracking().Where(x => x.OrderCode == code)
                .Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(cancellationToken);
            if (orderId.HasValue)
                await PostgresAdvisoryLock.AcquireAsync(context, $"trip-booking:order:{orderId:N}", cancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT order_id FROM orders WHERE order_code = {code} FOR UPDATE", cancellationToken);
        }
    }

    public async Task<AdminDashboardStatsRow> GetDashboardStatsAsync(Guid? scopeUserId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var soon = now.AddDays(7); var expiry = now.AddMinutes(15); var nowUtc = now.UtcDateTime;
        var trips = context.Trips.AsNoTracking().Where(x => !x.IsDeleted && (scopeUserId == null || x.ManagedByUserId == scopeUserId)
            && x.StartAt >= now && x.StartAt <= soon && x.Status != "Cancelled");
        var tripIds = trips.Select(x => x.TripId);
        var orders = OrderRepository.Scoped(context.Orders.AsNoTracking(), scopeUserId);
        var orderIds = orders.Select(x => x.OrderId);
        var row = new AdminDashboardStatsRow
        {
            PendingPaymentExpiringCount = await orders.CountAsync(x => x.Status == "PendingPayment" && x.HoldExpiresAt <= expiry && x.HoldExpiresAt > now, cancellationToken),
            TripsDepartingIn7DaysCount = await trips.CountAsync(cancellationToken),
            UnassignedParticipantsCount = await context.BookingParticipants.AsNoTracking().CountAsync(x => !x.IsDeleted && x.Status != "Cancelled"
                && x.TripGroupId == null && tripIds.Contains(x.TripId), cancellationToken),
            MissingProvidedItemsCount = await context.TripProvidedItems.AsNoTracking().CountAsync(x => tripIds.Contains(x.TripId)
                && x.RequiredQuantity > x.AssetAssignments.Count(a => a.Status == "Reserved" || a.Status == "Issued"), cancellationToken),
            UnderCapacityTripsCount = await trips.CountAsync(x => x.TripBookingOrders
                .Where(b => b.Order.Status == "Confirmed" || b.Order.Status == "InFulfillment").Sum(b => b.ParticipantCount) < x.MinParticipants, cancellationToken),
            OverdueRentalAssetsCount = await context.AssetAssignments.AsNoTracking().CountAsync(x => x.Status == "Issued" && x.RentalOrderItem != null
                && x.RentalOrderItem.RentalOrder.RentalEndAt < nowUtc && orderIds.Contains(x.RentalOrderItem.RentalOrderId), cancellationToken),
            NeedsReconciliationOrdersCount = await orders.CountAsync(x => x.PaymentStatus == "NeedsReconciliation", cancellationToken),
            DraftRentalChargesCount = await context.RentalCharges.AsNoTracking().CountAsync(x => x.Status == "Draft" && orderIds.Contains(x.RentalOrderId), cancellationToken)
        };
        if (scopeUserId == null)
        {
            row.PendingApprovalRefundsCount = await context.Payments.AsNoTracking().CountAsync(x => x.PaymentType == "Refund" && x.Status == "PendingApproval", cancellationToken);
            row.FailedRefundsCount = await context.Payments.AsNoTracking().CountAsync(x => x.PaymentType == "Refund" && x.Status == "Failed", cancellationToken);
        }
        return row;
    }

    public Task<List<MissingGearRow>> GetMissingGearAsync(Guid? scopeUserId, DateTimeOffset now, int take, CancellationToken cancellationToken = default)
    {
        var soon = now.AddDays(7);
        return context.TripProvidedItems.AsNoTracking()
            .Where(x => !x.Trip.IsDeleted && (scopeUserId == null || x.Trip.ManagedByUserId == scopeUserId) && x.Trip.Status != "Cancelled"
                && x.Trip.StartAt >= now && x.Trip.StartAt <= soon
                && x.RequiredQuantity > x.AssetAssignments.Count(a => a.Status == "Reserved" || a.Status == "Issued"))
            .OrderBy(x => x.Trip.StartAt).Take(take)
            .Select(x => new MissingGearRow
            {
                TripId = x.TripId,
                TourName = x.Trip.Tour.Name,
                RequiredQuantity = x.RequiredQuantity,
                AssignedQuantity = x.AssetAssignments.Count(a => a.Status == "Reserved" || a.Status == "Issued")
            }).ToListAsync(cancellationToken);
    }

    public Task<List<OrderRefRow>> GetOverdueRentalOrdersAsync(Guid? scopeUserId, DateTime now, int take, CancellationToken cancellationToken = default)
        => OrderRepository.Scoped(context.Orders.AsNoTracking(), scopeUserId)
            .Where(x => x.RentalOrder != null && x.RentalOrder.RentalEndAt < now
                && x.RentalOrder.RentalOrderItems.Any(i => i.AssetAssignments.Any(a => a.Status == "Issued")))
            .OrderBy(x => x.RentalOrder!.RentalEndAt).Take(take)
            .Select(x => new OrderRefRow { OrderId = x.OrderId, OrderCode = x.OrderCode }).ToListAsync(cancellationToken);

    public Task<List<OrderRefRow>> GetExpiringOrdersAsync(Guid? scopeUserId, DateTimeOffset now, DateTimeOffset until, int take, CancellationToken cancellationToken = default)
        => OrderRepository.Scoped(context.Orders.AsNoTracking(), scopeUserId)
            .Where(x => x.Status == "PendingPayment" && x.HoldExpiresAt > now && x.HoldExpiresAt <= until)
            .OrderBy(x => x.HoldExpiresAt).Take(take)
            .Select(x => new OrderRefRow { OrderId = x.OrderId, OrderCode = x.OrderCode }).ToListAsync(cancellationToken);

    public Task<List<OrderRefRow>> GetFailedRefundOrdersAsync(int take, CancellationToken cancellationToken = default)
        => context.Payments.AsNoTracking().Where(x => x.PaymentType == "Refund" && x.Status == "Failed")
            .OrderByDescending(x => x.CreatedAt).Take(take)
            .Select(x => new OrderRefRow { OrderId = x.OrderId, OrderCode = x.Order.OrderCode }).ToListAsync(cancellationToken);

    public async Task<PaymentTotalsRow> GetPaymentTotalsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var payments = context.Payments.AsNoTracking().Where(x => x.Status == "Succeeded" && x.ProcessedAt >= from && x.ProcessedAt <= to);
        return new PaymentTotalsRow
        {
            Collections = await payments.Where(x => x.PaymentType == "Collection").SumAsync(x => x.Amount, cancellationToken),
            Refunds = await payments.Where(x => x.PaymentType == "Refund").SumAsync(x => x.Amount, cancellationToken),
            AdditionalCharges = await payments.Where(x => x.PaymentType == "Collection" && x.Purpose == "AdditionalCharge").SumAsync(x => x.Amount, cancellationToken)
        };
    }

    public async Task<AdminKpiCountsRow> GetKpiCountsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => new()
        {
            BookingCount = await context.TripBookingOrders.AsNoTracking()
                .CountAsync(x => !x.Order.IsDeleted && x.Order.CreatedAt >= from && x.Order.CreatedAt <= to, cancellationToken),
            IssuedAssetCount = await context.AssetAssignments.AsNoTracking().Where(x => x.Status == "Issued")
                .Select(x => x.EquipmentAssetId).Distinct().CountAsync(cancellationToken),
            AvailableAssetCount = await context.EquipmentAssets.AsNoTracking()
                .CountAsync(x => !x.IsDeleted && x.AssetStatus == "Available", cancellationToken)
        };

    public Task<List<RevenuePaymentRow>> GetSucceededPaymentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => context.Payments.AsNoTracking().Where(x => x.Status == "Succeeded" && x.ProcessedAt >= from && x.ProcessedAt <= to)
            .Select(x => new RevenuePaymentRow { OrderType = x.Order.OrderType, PaymentType = x.PaymentType, Amount = x.Amount, ProcessedAt = x.ProcessedAt })
            .ToListAsync(cancellationToken);

    public Task<List<TripOccupancyRow>> GetOccupancyAsync(DateTimeOffset from, DateTimeOffset to, Guid? tourId, CancellationToken cancellationToken = default)
        => context.Trips.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status != "Cancelled" && x.StartAt >= from && x.StartAt <= to && (tourId == null || x.TourId == tourId))
            .Select(x => new TripOccupancyRow
            {
                TripId = x.TripId,
                TourName = x.Tour.Name,
                StartAt = x.StartAt,
                TotalSlots = x.TotalSlots,
                BookedCount = x.TripBookingOrders
                    .Where(b => b.Order.Status == "Confirmed" || b.Order.Status == "InFulfillment" || b.Order.Status == "Completed")
                    .Sum(b => b.ParticipantCount)
            }).ToListAsync(cancellationToken);

    public Task<List<AssetUsageRow>> GetAssetUsageAsync(DateTime from, DateTime to, Guid? categoryId, CancellationToken cancellationToken = default)
        => context.EquipmentAssets.AsNoTracking()
            .Where(x => !x.IsDeleted && (categoryId == null || x.ProductVariant.Product.CategoryId == categoryId))
            .Select(x => new AssetUsageRow
            {
                EquipmentAssetId = x.EquipmentAssetId,
                AssetCode = x.AssetCode,
                Periods = x.AssetAssignments
                    .Where(a => (a.Status == "Issued" || a.Status == "Returned" || a.Status == "LostOrException")
                        && a.AssignedAt < to && (a.ReturnedAt == null || a.ReturnedAt > from))
                    .Select(a => new AssetUsagePeriodRow { AssignedAt = a.AssignedAt, ReservedFrom = a.ReservedFrom, ReturnedAt = a.ReturnedAt })
                    .ToList()
            }).ToListAsync(cancellationToken);
}
