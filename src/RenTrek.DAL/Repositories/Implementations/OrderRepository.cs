using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Triển khai truy cập dữ liệu cho đơn hàng (danh sách đơn của khách) qua EF Core.
/// </summary>
public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<MyOrderRow> Items, int TotalCount)> GetMyOrdersPagedAsync(
        Guid customerUserId,
        MyOrderFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = FilteredMyOrders(customerUserId, filter);
        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Max(1, filter.PageSize);
        var rows = await Sort(query, filter)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new MyOrderQueryRow
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                OrderType = o.OrderType,
                Status = o.Status,
                PaymentStatus = o.CustomerUserId == customerUserId ? o.PaymentStatus : string.Empty,
                TotalAmount = o.CustomerUserId == customerUserId ? o.TotalAmount : 0m,
                HoldExpiresAt = o.CustomerUserId == customerUserId ? o.HoldExpiresAt : null,
                CreatedAt = o.CreatedAt,
                RentalStartAt = o.RentalOrder != null ? o.RentalOrder.RentalStartAt : null,
                RentalEndAt = o.RentalOrder != null ? o.RentalOrder.RentalEndAt : null,
                TripStartAt = o.TripBookingOrder != null ? o.TripBookingOrder.Trip.StartAt : null,
                TripEndAt = o.TripBookingOrder != null ? o.TripBookingOrder.Trip.EndAt : null,
                ItemCount = o.RentalOrder != null ? o.RentalOrder.RentalOrderItems.Count(i => !i.IsDeleted) : 0,
                FirstItemName = o.RentalOrder == null
                    ? null
                    : o.RentalOrder.RentalOrderItems
                        .Where(i => !i.IsDeleted)
                        .OrderBy(i => i.CreatedAt).ThenBy(i => i.RentalOrderItemId)
                        .Select(i => i.Bundle != null
                            ? i.Bundle.Name
                            : i.ProductVariant!.Product.Name + " — " + i.ProductVariant.Name)
                        .FirstOrDefault(),
                TourName = o.TripBookingOrder != null ? o.TripBookingOrder.Trip.Tour.Name : null
            })
            .ToListAsync(cancellationToken);

        // Đơn thuê lưu DateTime (UTC), đơn chuyến lưu DateTimeOffset: gộp về DateTimeOffset ở bộ nhớ
        var items = rows.Select(r => new MyOrderRow
        {
            OrderId = r.OrderId,
            OrderCode = r.OrderCode,
            OrderType = r.OrderType,
            Status = r.Status,
            PaymentStatus = r.PaymentStatus,
            TotalAmount = r.TotalAmount,
            HoldExpiresAt = r.HoldExpiresAt,
            CreatedAt = r.CreatedAt,
            ServiceStartAt = r.RentalStartAt.HasValue ? ToUtcOffset(r.RentalStartAt.Value) : r.TripStartAt,
            ServiceEndAt = r.RentalEndAt.HasValue ? ToUtcOffset(r.RentalEndAt.Value) : r.TripEndAt,
            ItemCount = r.ItemCount,
            FirstItemName = r.FirstItemName,
            TourName = r.TourName
        }).ToList();

        return (items, totalCount);
    }

    private IQueryable<Order> FilteredMyOrders(Guid customerUserId, MyOrderFilter filter)
    {
        // Bỏ query filter để đơn lịch sử vẫn đọc được tên sản phẩm / chuyến đã bị xoá mềm; tự lọc đơn chưa xoá
        var query = _dbSet.IgnoreQueryFilters().AsNoTracking()
            .Where(o => !o.IsDeleted &&
                        (o.CustomerUserId == customerUserId ||
                         (o.TripBookingOrder != null &&
                          o.TripBookingOrder.Participants.Any(p =>
                              !p.IsDeleted &&
                              p.LinkedUserId == customerUserId &&
                              p.LinkedUser != null &&
                              !p.LinkedUser.IsDeleted &&
                              p.LinkedUser.Status == "Active" && p.LinkedUser.EmailConfirmedAt != null))));

        if (!string.IsNullOrWhiteSpace(filter.OrderType))
        {
            var orderType = filter.OrderType.Trim();
            query = query.Where(o => o.OrderType == orderType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(o => o.OrderCode.ToLower().Contains(search));
        }

        return query;
    }

    // Sắp theo thời điểm bắt đầu dịch vụ: đơn thuê (DateTime, ép về DateTimeOffset => ::timestamptz) hoặc giờ khởi hành của chuyến
    private static IQueryable<Order> Sort(IQueryable<Order> query, MyOrderFilter filter)
    {
        var desc = filter.IsDescending;
        if (string.Equals(filter.SortBy, "serviceStartAt", StringComparison.OrdinalIgnoreCase))
        {
            return desc
                ? query.OrderByDescending(o => o.RentalOrder != null
                        ? (DateTimeOffset?)o.RentalOrder.RentalStartAt
                        : (o.TripBookingOrder != null ? (DateTimeOffset?)o.TripBookingOrder.Trip.StartAt : null))
                    .ThenByDescending(o => o.OrderId)
                : query.OrderBy(o => o.RentalOrder != null
                        ? (DateTimeOffset?)o.RentalOrder.RentalStartAt
                        : (o.TripBookingOrder != null ? (DateTimeOffset?)o.TripBookingOrder.Trip.StartAt : null))
                    .ThenBy(o => o.OrderId);
        }

        return desc
            ? query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OrderId)
            : query.OrderBy(o => o.CreatedAt).ThenBy(o => o.OrderId);
    }

    public Task<(IReadOnlyList<AdminOrderRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminOrderFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Scoped(_dbSet.AsNoTracking(), filter.ScopeUserId);
        if (filter.OrderType != null) query = query.Where(x => x.OrderType == filter.OrderType);
        if (filter.Status != null) query = query.Where(x => x.Status == filter.Status);
        if (filter.PaymentStatus != null) query = query.Where(x => x.PaymentStatus == filter.PaymentStatus);
        if (filter.Tab is "PendingPayment" or "Cancelled") query = query.Where(x => x.Status == filter.Tab);
        if (filter.Tab == "NeedsReconciliation") query = query.Where(x => x.PaymentStatus == "NeedsReconciliation");
        if (filter.Tab == "PendingRefund")
            query = query.Where(x => x.Payments.Any(p => p.PaymentType == "Refund" && (p.Status == "PendingApproval" || p.Status == "Pending")));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(x => x.OrderCode.ToLower().Contains(search) || x.CustomerUser.Email.ToLower().Contains(search));
        }

        return query
            .SortBy(filter.SortBy, filter.IsDescending, nameof(Order.CreatedAt), nameof(Order.OrderCode),
                nameof(Order.TotalAmount), nameof(Order.Status), nameof(Order.HoldExpiresAt), nameof(Order.ReconciliationDeadlineAt))
            .ToPageAsync(filter.PageNumber, filter.PageSize, ToAdminRow, cancellationToken);
    }

    public Task<AdminOrderRow?> GetAdminRowAsync(string orderCode, Guid? scopeUserId, CancellationToken cancellationToken = default)
        => Scoped(_dbSet.AsNoTracking(), scopeUserId).Where(x => x.OrderCode == orderCode)
            .Select(ToAdminRow).SingleOrDefaultAsync(cancellationToken);

    public Task<string?> GetOrderCodeAsync(Guid orderId, Guid? scopeUserId, CancellationToken cancellationToken = default)
        => Scoped(_dbSet.AsNoTracking(), scopeUserId).Where(x => x.OrderId == orderId)
            .Select(x => x.OrderCode).SingleOrDefaultAsync(cancellationToken);

    public Task<Order?> GetAdminForUpdateAsync(string orderCode, Guid? scopeUserId, CancellationToken cancellationToken = default)
        => Scoped(_dbSet, scopeUserId)
            .Include(x => x.Payments)
            .Include(x => x.TripBookingOrder)
                .ThenInclude(t => t.Trip)
                .ThenInclude(t => t.Tour)
            .SingleOrDefaultAsync(x => x.OrderCode == orderCode, cancellationToken);

    public Task<List<BookingParticipant>> GetParticipantsAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Set<BookingParticipant>().Where(x => !x.IsDeleted && x.BookingOrderId == orderId).ToListAsync(cancellationToken);

    public Task<BookingParticipant?> GetParticipantForUpdateAsync(Guid orderId, Guid participantId, CancellationToken cancellationToken = default)
        => _context.Set<BookingParticipant>().Include(x => x.Trip)
            .SingleOrDefaultAsync(x => x.BookingOrderId == orderId && x.ParticipantId == participantId && !x.IsDeleted, cancellationToken);

    public Task<List<Payment>> GetPaymentsAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Set<Payment>().AsNoTracking().Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task<Payment?> GetManualCollectionAsync(string reference, CancellationToken cancellationToken = default)
        => _context.Set<Payment>().AsNoTracking().SingleOrDefaultAsync(x => x.PaymentMethod == "Manual"
            && x.PaymentType == "Collection" && x.ProviderTransactionId == reference, cancellationToken);

    public Task<List<RentalOrderItem>> GetRentalLinesAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Set<RentalOrderItem>().AsNoTracking().AsSplitQuery()
            .Where(x => !x.IsDeleted && x.RentalOrderId == orderId)
            .Include(x => x.Bundle)
            .Include(x => x.ProductVariant)
            .Include(x => x.RentalBundleItemSnapshots).ThenInclude(x => x.ProductVariant)
            .Include(x => x.AssetAssignments).ThenInclude(x => x.EquipmentAsset)
            .ToListAsync(cancellationToken);

    public Task<RentalOrder?> GetRentalWithHandlerAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Set<RentalOrder>().AsNoTracking().Include(x => x.HandledByUser)
            .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);

    public Task<List<AppliedPromotion>> GetAppliedPromotionsAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Set<AppliedPromotion>().Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);

    public Task<List<AssetAssignment>> GetAssignmentsForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Set<AssetAssignment>()
            .Where(x => (x.RentalOrderItem != null && x.RentalOrderItem.RentalOrderId == orderId)
                || (x.AppliedPromotion != null && x.AppliedPromotion.OrderId == orderId))
            .ToListAsync(cancellationToken);

    public Task<List<ParticipantInvitation>> GetOpenInvitationsForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _context.Set<ParticipantInvitation>()
            .Where(x => x.BookingOrderId == orderId && x.RevokedAt == null && x.RedeemedAt == null)
            .ToListAsync(cancellationToken);

    private static readonly Expression<Func<Order, AdminOrderRow>> ToAdminRow = x => new AdminOrderRow
    {
        OrderId = x.OrderId,
        OrderCode = x.OrderCode,
        OrderType = x.OrderType,
        CustomerUserId = x.CustomerUserId,
        CustomerUserCode = x.CustomerUser.Profile == null ? null : x.CustomerUser.Profile.UserCode,
        CustomerFullName = x.CustomerUser.FullName,
        CustomerEmail = x.CustomerUser.Email,
        CustomerPhone = x.CustomerUser.Phone,
        TourName = x.TripBookingOrder == null ? null : x.TripBookingOrder.Trip.Tour.Name,
        ParticipantCount = x.TripBookingOrder == null ? 0 : x.TripBookingOrder.ParticipantCount,
        RentalItemQuantity = x.RentalOrder == null ? 0 : x.RentalOrder.RentalOrderItems.Where(i => !i.IsDeleted).Sum(i => i.Quantity),
        TotalAmount = x.TotalAmount,
        Status = x.Status,
        PaymentStatus = x.PaymentStatus,
        HoldExpiresAt = x.HoldExpiresAt,
        ReconciliationDeadlineAt = x.ReconciliationDeadlineAt,
        HasPendingRefund = x.Payments.Any(p => p.PaymentType == "Refund" && (p.Status == "PendingApproval" || p.Status == "Pending")),
        CreatedAt = x.CreatedAt
    };

    /// <summary>null = mọi đơn; ngược lại chỉ đơn có chuyến do người này quản lý hoặc đơn thuê họ phụ trách.</summary>
    /// <summary>Giới hạn đơn theo staff phụ trách (chuyến quản lý / đơn thuê xử lý); null = không giới hạn.</summary>
    internal static IQueryable<Order> Scoped(IQueryable<Order> query, Guid? scopeUserId)
        => scopeUserId is not { } userId ? query : query.Where(x =>
            (x.OrderType == "TripBooking" && x.TripBookingOrder.Trip.ManagedByUserId == userId)
            || (x.OrderType == "Rental" && x.RentalOrder != null && x.RentalOrder.HandledByUserId == userId));

    private static DateTimeOffset ToUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private sealed class MyOrderQueryRow
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTimeOffset? HoldExpiresAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTime? RentalStartAt { get; set; }
        public DateTime? RentalEndAt { get; set; }
        public DateTimeOffset? TripStartAt { get; set; }
        public DateTimeOffset? TripEndAt { get; set; }
        public int ItemCount { get; set; }
        public string? FirstItemName { get; set; }
        public string? TourName { get; set; }
    }
}
