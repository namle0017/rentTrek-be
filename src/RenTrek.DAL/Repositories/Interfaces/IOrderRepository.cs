using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    /// <summary>
    /// Danh sách đơn của khách (đơn thuê và đơn đặt chuyến) đã lọc / sắp xếp / phân trang, chiếu thẳng trong SQL
    /// (tên dòng đầu tiên, số dòng, tên tour) để tránh N+1.
    /// </summary>
    Task<(IReadOnlyList<MyOrderRow> Items, int TotalCount)> GetMyOrdersPagedAsync(
        Guid customerUserId,
        MyOrderFilter filter,
        CancellationToken cancellationToken = default);
}
