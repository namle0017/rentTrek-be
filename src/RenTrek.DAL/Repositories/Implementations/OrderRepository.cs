using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
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
                PaymentStatus = o.PaymentStatus,
                TotalAmount = o.TotalAmount,
                HoldExpiresAt = o.HoldExpiresAt,
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
            .Where(o => o.CustomerUserId == customerUserId && !o.IsDeleted);

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
