using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
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

    public async Task<RentalOrderDetailRow?> GetOwnedDetailAsync(
        Guid orderId,
        Guid customerUserId,
        CancellationToken cancellationToken = default)
    {
        // Bỏ query filter để đơn lịch sử vẫn đọc được tên sản phẩm / tài khoản đã xoá mềm; tự lọc đơn chưa xoá
        var detail = await _context.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.OrderId == orderId
                        && o.CustomerUserId == customerUserId
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

    public async Task<RentalOrder?> GetByOrderCodeWithFulfillmentAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return await _context.RentalOrders
            .Include(ro => ro.Order)
                .ThenInclude(o => o.CustomerUser)
            .Include(ro => ro.RentalOrderItems.Where(roi => !roi.IsDeleted))
                .ThenInclude(roi => roi.ProductVariant)
                    .ThenInclude(pv => pv!.Product)
            .Include(ro => ro.RentalOrderItems.Where(roi => !roi.IsDeleted))
                .ThenInclude(roi => roi.Bundle)
            .Include(ro => ro.RentalOrderItems.Where(roi => !roi.IsDeleted))
                .ThenInclude(roi => roi.AssetAssignments)
                    .ThenInclude(a => a.EquipmentAsset)
                        .ThenInclude(ea => ea.ProductVariant)
            .Include(ro => ro.RentalCharges)
                .ThenInclude(rc => rc.EquipmentAsset)
            .Include(ro => ro.RentalCharges)
                .ThenInclude(rc => rc.CreatedByUser)
            .FirstOrDefaultAsync(ro => ro.Order.OrderCode == orderCode, cancellationToken);
    }
}
