using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho đơn thuê thiết bị (idempotency, chi tiết đơn, tạo đơn) qua EF Core.
/// </summary>
public class RentalOrderRepository : GenericRepository<RentalOrder>, IRentalOrderRepository
{
    // Giá trị orders.order_type của đơn thuê (xem RenTrek.BLL.Constants.OrderTypes.Rental)
    private const string RentalOrderType = "Rental";

    // Trùng giá trị RenTrek.BLL.Constants (OrderStatuses, OrderPaymentStatuses, AssignmentStatuses); DAL không tham chiếu BLL
    private const string ConfirmedStatus = "Confirmed";
    private const string InFulfillmentStatus = "InFulfillment";
    private const string AwaitingSettlementStatus = "AwaitingSettlement";
    private const string PaidStatus = "Paid";
    private const string IssuedAssignmentStatus = "Issued";
    private const int LifecycleBatchSize = 200;

    public RentalOrderRepository(AppDbContext context) : base(context)
    {
    }

    public Task AcquireIdempotencyLockAsync(
        Guid customerUserId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(
            _context, $"rental-order:idempotency:{customerUserId:N}:{idempotencyKey}", cancellationToken);

    public Task<Order?> GetByIdempotencyKeyAsync(
        Guid customerUserId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => _context.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.RentalOrder!).ThenInclude(r => r.RentalOrderItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ProductVariant!).ThenInclude(v => v.Product)
            .Include(o => o.RentalOrder!).ThenInclude(r => r.RentalOrderItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Bundle)
            .Include(o => o.RentalOrder!).ThenInclude(r => r.RentalOrderItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.RentalBundleItemSnapshots).ThenInclude(s => s.ProductVariant).ThenInclude(v => v.Product)
            .Include(o => o.AppliedPromotions)
            .Where(o => o.CustomerUserId == customerUserId
                        && o.IdempotencyKey == idempotencyKey
                        && !o.IsDeleted
                        && o.OrderType == RentalOrderType)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RentalOrderDetailRow?> GetOwnedDetailAsync(
        Guid orderId,
        Guid customerUserId,
        CancellationToken cancellationToken = default)
        => LoadDetailAsync(orderId, customerUserId, cancellationToken);

    public Task<RentalOrderDetailRow?> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
        => LoadDetailAsync(orderId, null, cancellationToken);

    public Task AcquireFulfillmentLockAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(_context, $"rental-order:fulfillment:{orderId:N}", cancellationToken);

    public async Task<Order?> GetForFulfillmentAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // Khóa dòng orders giống AdminOperationRepository ("order:" lock) để khách hủy / nhận-trả không ghi đè thao tác admin đang chạy
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT order_id FROM orders WHERE order_id = {orderId} FOR UPDATE", cancellationToken);
        return await _context.Orders
            .Include(o => o.RentalOrder!).ThenInclude(r => r.RentalOrderItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.AssetAssignments).ThenInclude(a => a.EquipmentAsset)
            .Include(o => o.RentalOrder!).ThenInclude(r => r.RentalCharges)
            .Include(o => o.AppliedPromotions).ThenInclude(p => p.AssetAssignments)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.OrderId == orderId
                                      && !o.IsDeleted
                                      && o.OrderType == RentalOrderType
                                      && o.RentalOrder != null, cancellationToken);
    }

    // customerUserId null = màn vận hành (Staff/Admin) đọc mọi đơn
    private async Task<RentalOrderDetailRow?> LoadDetailAsync(
        Guid orderId,
        Guid? customerUserId,
        CancellationToken cancellationToken)
    {
        // Bỏ query filter để đơn lịch sử vẫn đọc được tên sản phẩm / tài khoản đã xoá mềm; tự lọc đơn chưa xoá
        var detail = await _context.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.OrderId == orderId
                        && (customerUserId == null || o.CustomerUserId == customerUserId)
                        && !o.IsDeleted
                        && o.OrderType == RentalOrderType
                        && o.RentalOrder != null)
            .Select(o => new RentalOrderDetailRow
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                OrderType = o.OrderType,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus,
                CreatedAt = o.CreatedAt,
                HoldExpiresAt = o.HoldExpiresAt,
                Note = o.Note,
                CancelledAt = o.CancelledAt,
                CancelReason = o.CancelReason,
                SubtotalAmount = o.SubtotalAmount,
                DiscountAmount = o.DiscountAmount,
                TotalAmount = o.TotalAmount,
                RentalStartAt = o.RentalOrder!.RentalStartAt,
                RentalEndAt = o.RentalOrder.RentalEndAt,
                DepositAmount = o.RentalOrder.DepositAmount,
                CustomerUserId = o.CustomerUserId,
                RentalPolicyId = o.RentalOrder.RentalPolicyId,
                HandedOverAt = o.RentalOrder.HandedOverAt,
                CustomerReceivedConfirmedAt = o.RentalOrder.CustomerReceivedConfirmedAt,
                ReceiptConfirmSource = o.RentalOrder.ReceiptConfirmSource,
                CancellationType = o.RentalOrder.CancellationType,
                CustomerName = o.CustomerUser.FullName,
                CustomerEmail = o.CustomerUser.Email,
                CustomerPhone = o.CustomerUser.Phone,
                HandledByName = o.RentalOrder.HandledByUser != null ? o.RentalOrder.HandledByUser.FullName : null,
                RefundPolicyId = o.RefundPolicy.RefundPolicyId,
                RefundPolicyName = o.RefundPolicy.Name,
                RefundPolicyVersion = o.RefundPolicy.Version
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (detail is null)
            return null;

        detail.Lines = await _context.Set<RentalOrderItem>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.RentalOrderId == orderId && !i.IsDeleted)
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.RentalOrderItemId)
            .Select(i => new RentalOrderLineRow
            {
                ItemId = i.RentalOrderItemId,
                VariantId = i.ProductVariantId,
                BundleId = i.BundleId,
                BundleName = i.Bundle != null ? i.Bundle.Name : null,
                ProductName = i.ProductVariant != null ? i.ProductVariant.Product.Name : null,
                VariantName = i.ProductVariant != null ? i.ProductVariant.Name : null,
                Sku = i.ProductVariant != null ? i.ProductVariant.Sku : null,
                Quantity = i.Quantity,
                DailyPrice = i.DailyPrice,
                BillableDays = i.BillableDays,
                LineAmount = i.LineAmount,
                Components = i.RentalBundleItemSnapshots
                    .OrderBy(s => s.CreatedAt).ThenBy(s => s.RentalBundleItemSnapshotId)
                    .Select(s => new RentalOrderLineComponentRow
                    {
                        VariantId = s.ProductVariantId,
                        ProductName = s.ProductVariant.Product.Name,
                        VariantName = s.ProductVariant.Name,
                        QuantityPerBundle = s.QuantityPerBundle
                    })
                    .ToList()
            })
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        detail.Assignments = await _context.Set<AssetAssignment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.RentalOrderItem != null && a.RentalOrderItem.RentalOrderId == orderId)
            .OrderBy(a => a.AssignedAt).ThenBy(a => a.AssetAssignmentId)
            .Select(a => new RentalOrderAssignmentRow
            {
                AssignmentId = a.AssetAssignmentId,
                RentalOrderItemId = a.RentalOrderItemId,
                EquipmentAssetId = a.EquipmentAssetId,
                AssetStatus = a.EquipmentAsset.AssetStatus,
                Status = a.Status,
                AssignedAt = a.AssignedAt,
                ReturnedAt = a.ReturnedAt,
                ReturnCondition = a.ReturnCondition,
                ReservedFrom = a.ReservedFrom,
                ReservedUntil = a.ReservedUntil,
                AssetCode = a.EquipmentAsset.AssetCode,
                ProductName = a.EquipmentAsset.ProductVariant.Product.Name,
                VariantName = a.EquipmentAsset.ProductVariant.Name
            })
            .ToListAsync(cancellationToken);

        detail.Charges = await _context.Set<RentalCharge>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.RentalOrderId == orderId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.RentalChargeId)
            .Select(c => new RentalOrderChargeRow
            {
                Id = c.RentalChargeId,
                ChargeType = c.ChargeType,
                Amount = c.Amount,
                Reason = c.Reason,
                AssetCode = c.EquipmentAsset != null ? c.EquipmentAsset.AssetCode : null,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                VoidedAt = c.VoidedAt
            })
            .ToListAsync(cancellationToken);

        detail.Payments = await _context.Payments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.PaymentId)
            .Select(p => new RentalOrderPaymentRow
            {
                Id = p.PaymentId,
                PaymentType = p.PaymentType,
                Purpose = p.Purpose,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                ProcessedAt = p.ProcessedAt
            })
            .ToListAsync(cancellationToken);

        return detail;
    }

    public Task<bool> OrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default)
        => _context.Orders.IgnoreQueryFilters().AnyAsync(o => o.OrderCode == orderCode, cancellationToken);

    public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default)
        => _context.Orders.AddAsync(order, cancellationToken).AsTask();

    public Task<(IReadOnlyList<AdminRentalOrderRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminRentalOrderFilter filter,
        CancellationToken cancellationToken = default)
    {
        // Giá trị asset_assignments.status / rental_charges.status (xem RenTrek.BLL.Constants)
        const string Issued = "Issued", Returned = "Returned", Lost = "LostOrException", Cancelled = "Cancelled", Draft = "Draft";
        var now = filter.Now;

        var orders = _context.Orders.AsNoTracking()
            .Where(o => !o.IsDeleted && o.OrderType == RentalOrderType && o.RentalOrder != null);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            orders = orders.Where(o => o.Status == filter.Status);

        if (filter.OverdueOnly)
            orders = orders.Where(o => o.RentalOrder!.RentalEndAt < now
                                       && o.RentalOrder.RentalOrderItems.Any(i => !i.IsDeleted
                                           && i.AssetAssignments.Any(a => a.Status == Issued)));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            orders = orders.Where(o => o.OrderCode.ToLower().Contains(search)
                                       || o.CustomerUser.FullName.ToLower().Contains(search)
                                       || o.CustomerUser.Email.ToLower().Contains(search));
        }

        return orders
            .OrderByDescending(o => o.RentalOrder!.RentalStartAt)
            .ThenBy(o => o.OrderId)
            .ToPageAsync(filter.PageNumber, filter.PageSize, o => new AdminRentalOrderRow
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                CustomerName = o.CustomerUser.FullName,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus,
                RentalStartAt = o.RentalOrder!.RentalStartAt,
                RentalEndAt = o.RentalOrder.RentalEndAt,
                TotalAmount = o.TotalAmount,
                AssetsCount = o.RentalOrder.RentalOrderItems.Where(i => !i.IsDeleted)
                    .SelectMany(i => i.AssetAssignments).Count(a => a.Status != Cancelled),
                IssuedCount = o.RentalOrder.RentalOrderItems.Where(i => !i.IsDeleted)
                    .SelectMany(i => i.AssetAssignments).Count(a => a.Status == Issued),
                ReturnedCount = o.RentalOrder.RentalOrderItems.Where(i => !i.IsDeleted)
                    .SelectMany(i => i.AssetAssignments).Count(a => a.Status == Returned || a.Status == Lost),
                DraftChargesCount = o.RentalOrder.RentalCharges.Count(c => c.Status == Draft),
                IsOverdue = o.RentalOrder.RentalEndAt < now
                            && o.RentalOrder.RentalOrderItems.Any(i => !i.IsDeleted
                                && i.AssetAssignments.Any(a => a.Status == Issued))
            }, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminRentalScheduleRow>> GetHandoverScheduleAsync(
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        CancellationToken cancellationToken = default)
    {
        const string Confirmed = "Confirmed", InFulfillment = "InFulfillment", Reserved = "Reserved";

        return await _context.Set<AssetAssignment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.RentalOrderItem != null
                        && !a.RentalOrderItem.IsDeleted
                        && !a.RentalOrderItem.RentalOrder.Order.IsDeleted
                        && a.RentalOrderItem.RentalOrder.Order.OrderType == RentalOrderType
                        && (a.RentalOrderItem.RentalOrder.Order.Status == Confirmed
                            || a.RentalOrderItem.RentalOrder.Order.Status == InFulfillment)
                        && a.Status == Reserved
                        && a.RentalOrderItem.RentalOrder.RentalStartAt >= rangeStartUtc
                        && a.RentalOrderItem.RentalOrder.RentalStartAt < rangeEndUtc)
            .OrderBy(a => a.RentalOrderItem!.RentalOrder.RentalStartAt)
            .ThenBy(a => a.RentalOrderItem!.RentalOrder.Order.OrderCode)
            .ThenBy(a => a.EquipmentAsset.AssetCode)
            .Select(a => new AdminRentalScheduleRow
            {
                OrderId = a.RentalOrderItem!.RentalOrderId,
                OrderCode = a.RentalOrderItem.RentalOrder.Order.OrderCode,
                CustomerName = a.RentalOrderItem.RentalOrder.Order.CustomerUser.FullName,
                CustomerPhone = a.RentalOrderItem.RentalOrder.Order.CustomerUser.Phone,
                ProductName = a.EquipmentAsset.ProductVariant.Product.Name,
                VariantName = a.EquipmentAsset.ProductVariant.Name,
                AssetCode = a.EquipmentAsset.AssetCode,
                ScheduledAt = a.RentalOrderItem.RentalOrder.RentalStartAt,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminRentalScheduleRow>> GetReturnScheduleAsync(
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        CancellationToken cancellationToken = default)
    {
        const string Confirmed = "Confirmed", InFulfillment = "InFulfillment", Issued = "Issued";

        return await _context.Set<AssetAssignment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.RentalOrderItem != null
                        && !a.RentalOrderItem.IsDeleted
                        && !a.RentalOrderItem.RentalOrder.Order.IsDeleted
                        && a.RentalOrderItem.RentalOrder.Order.OrderType == RentalOrderType
                        && (a.RentalOrderItem.RentalOrder.Order.Status == Confirmed
                            || a.RentalOrderItem.RentalOrder.Order.Status == InFulfillment)
                        && a.Status == Issued
                        && a.ReturnedAt == null
                        && a.RentalOrderItem.RentalOrder.RentalEndAt >= rangeStartUtc
                        && a.RentalOrderItem.RentalOrder.RentalEndAt < rangeEndUtc)
            .OrderBy(a => a.RentalOrderItem!.RentalOrder.RentalEndAt)
            .ThenBy(a => a.RentalOrderItem!.RentalOrder.Order.OrderCode)
            .ThenBy(a => a.EquipmentAsset.AssetCode)
            .Select(a => new AdminRentalScheduleRow
            {
                OrderId = a.RentalOrderItem!.RentalOrderId,
                OrderCode = a.RentalOrderItem.RentalOrder.Order.OrderCode,
                CustomerName = a.RentalOrderItem.RentalOrder.Order.CustomerUser.FullName,
                CustomerPhone = a.RentalOrderItem.RentalOrder.Order.CustomerUser.Phone,
                ProductName = a.EquipmentAsset.ProductVariant.Product.Name,
                VariantName = a.EquipmentAsset.ProductVariant.Name,
                AssetCode = a.EquipmentAsset.AssetCode,
                ScheduledAt = a.RentalOrderItem.RentalOrder.RentalEndAt,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);
    }

    public Task AddAdjustmentAsync(RentalOrderAdjustment adjustment, CancellationToken cancellationToken = default)
        => _context.RentalOrderAdjustments.AddAsync(adjustment, cancellationToken).AsTask();

    public Task<RentalOrderAdjustment?> GetAdjustmentByPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => _context.RentalOrderAdjustments
            .FirstOrDefaultAsync(a => a.PaymentId == paymentId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetPickupOverdueOrderIdsAsync(DateTime startedBeforeUtc, CancellationToken cancellationToken = default)
        => await RentalOrdersQuery()
            .Where(o => o.Status == ConfirmedStatus
                        && o.PaymentStatus == PaidStatus
                        && o.RentalOrder!.HandedOverAt == null
                        && o.RentalOrder.RentalStartAt <= startedBeforeUtc)
            .OrderBy(o => o.RentalOrder!.RentalStartAt)
            .Select(o => o.OrderId)
            .Take(LifecycleBatchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetReturnDueOrderIdsAsync(DateTime endFromUtc, DateTime endToUtc, CancellationToken cancellationToken = default)
        => await RentalOrdersQuery()
            .Where(o => o.Status == InFulfillmentStatus
                        && o.RentalOrder!.RentalEndAt >= endFromUtc
                        && o.RentalOrder.RentalEndAt <= endToUtc)
            .Select(o => o.OrderId)
            .Take(LifecycleBatchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetReturnOverdueOrderIdsAsync(DateTime endedBeforeUtc, CancellationToken cancellationToken = default)
        => await RentalOrdersQuery()
            .Where(o => o.Status == InFulfillmentStatus
                        && o.RentalOrder!.RentalEndAt <= endedBeforeUtc
                        && o.RentalOrder.RentalOrderItems.Any(i => !i.IsDeleted
                            && i.AssetAssignments.Any(a => a.Status == IssuedAssignmentStatus)))
            .OrderBy(o => o.RentalOrder!.RentalEndAt)
            .Select(o => o.OrderId)
            .Take(LifecycleBatchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetAwaitingReceiptConfirmOrderIdsAsync(DateTime handedOverBeforeUtc, CancellationToken cancellationToken = default)
        => await RentalOrdersQuery()
            .Where(o => o.RentalOrder!.HandedOverAt != null
                        && o.RentalOrder.HandedOverAt <= handedOverBeforeUtc
                        && o.RentalOrder.CustomerReceivedConfirmedAt == null)
            .Select(o => o.OrderId)
            .Take(LifecycleBatchSize)
            .ToListAsync(cancellationToken);

    public Task<int> CountCancellationsAsync(
        Guid customerUserId,
        IReadOnlyCollection<string> cancellationTypes,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken = default)
        => RentalOrdersQuery()
            .Where(o => o.CustomerUserId == customerUserId
                        && o.CancelledAt != null
                        && o.CancelledAt >= sinceUtc
                        && o.RentalOrder!.CancellationType != null
                        && cancellationTypes.Contains(o.RentalOrder.CancellationType))
            .CountAsync(cancellationToken);

    public Task<bool> HasOutstandingDebtAsync(Guid customerUserId, CancellationToken cancellationToken = default)
        => RentalOrdersQuery()
            .AnyAsync(o => o.CustomerUserId == customerUserId && o.Status == AwaitingSettlementStatus, cancellationToken);

    public async Task<IReadOnlyList<RentalOrderAdjustment>> GetStalePendingAdjustmentsAsync(DateTime createdBeforeUtc, CancellationToken cancellationToken = default)
        => await _context.RentalOrderAdjustments
            .AsNoTracking()
            .Where(a => a.Status == "Pending" && a.CreatedAt <= createdBeforeUtc)
            .OrderBy(a => a.CreatedAt)
            .Take(LifecycleBatchSize)
            .ToListAsync(cancellationToken);

    public Task<RentalOrderAdjustment?> GetPendingAdjustmentAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.RentalOrderAdjustments
            .FirstOrDefaultAsync(a => a.RentalOrderId == orderId && a.Status == "Pending", cancellationToken);

    private IQueryable<Order> RentalOrdersQuery()
        => _context.Orders
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.OrderType == RentalOrderType && o.RentalOrder != null);

    public async Task<List<RentalOrderAdjustmentRow>?> GetAdjustmentsAsync(
        Guid orderId,
        Guid? customerUserId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _context.Orders
            .IgnoreQueryFilters()
            .AnyAsync(o => o.OrderId == orderId
                           && (customerUserId == null || o.CustomerUserId == customerUserId)
                           && !o.IsDeleted
                           && o.OrderType == RentalOrderType
                           && o.RentalOrder != null, cancellationToken);
        if (!exists)
            return null;

        return await _context.RentalOrderAdjustments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.RentalOrderId == orderId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.RentalOrderAdjustmentId)
            .Select(x => new RentalOrderAdjustmentRow
            {
                Id = x.RentalOrderAdjustmentId,
                AdjustmentType = x.AdjustmentType,
                OldEndAt = x.OldEndAt,
                NewEndAt = x.NewEndAt,
                ExtraAmount = x.ExtraAmount,
                RequestedByUserId = x.RequestedByUserId,
                RequestedByName = x.RequestedByUser.FullName,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
