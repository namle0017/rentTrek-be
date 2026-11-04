using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRentalOrderRepository : IGenericRepository<RentalOrder>
{
    /// <summary>
    /// Khoá advisory theo transaction cho cặp (khách, Idempotency-Key) để hai request cùng key tạo đơn tuần tự
    /// (khoá <c>rental-order:idempotency:{user:N}:{key}</c>). BẮT BUỘC gọi trong transaction đã mở.
    /// </summary>
    Task AcquireIdempotencyLockAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đơn thuê (chưa xoá) đã tạo với Idempotency-Key này, kèm RentalOrder, các dòng (+biến thể / sản phẩm / combo),
    /// snapshot thành phần combo và khuyến mãi đã áp. Entity chỉ đọc (AsNoTracking); null nếu chưa có.
    /// </summary>
    Task<Order?> GetByIdempotencyKeyAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chi tiết đơn thuê thuộc khách (dòng + snapshot combo, phân bổ tài sản, phí, thanh toán, chính sách hoàn tiền);
    /// null nếu đơn không tồn tại, đã xoá, không phải đơn thuê hoặc không thuộc khách.
    /// </summary>
    Task<RentalOrderDetailRow?> GetOwnedDetailAsync(Guid orderId, Guid customerUserId, CancellationToken cancellationToken = default);

    /// <summary>Như <see cref="GetOwnedDetailAsync"/> nhưng không lọc theo khách (màn vận hành Staff/Admin).</summary>
    Task<RentalOrderDetailRow?> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá advisory theo đơn (<c>rental-order:fulfillment:{orderId:N}</c>) để giao / trả / lập phí tuần tự,
    /// tránh trả hai lần hay tạo trùng phí trễ khi bấm đúp. BẮT BUỘC gọi trong transaction đã mở.
    /// </summary>
    Task AcquireFulfillmentLockAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Đơn thuê có tracking kèm dòng, phân bổ (+thiết bị) và phí để cập nhật; null nếu không có.</summary>
    Task<Order?> GetForFulfillmentAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Mã đơn đã tồn tại (kể cả đơn đã xoá mềm) hay chưa.</summary>
    Task<bool> OrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default);

    /// <summary>Thêm cả đồ thị đơn (Order + RentalOrder + dòng + snapshot + khuyến mãi...) vào context; chưa SaveChanges.</summary>
    Task AddOrderAsync(Order order, CancellationToken cancellationToken = default);
}
