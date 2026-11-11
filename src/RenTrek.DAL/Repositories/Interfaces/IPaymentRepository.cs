using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IPaymentRepository : IGenericRepository<Payment>
{
    /// <summary>
    /// Khoá advisory theo transaction cho một đơn (khoá <c>payment:order:{orderId:N}</c>) để tạo thanh toán và xử lý webhook
    /// của cùng đơn chạy tuần tự. BẮT BUỘC gọi trong transaction đã mở.
    /// </summary>
    Task AcquireOrderLockAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Đơn (có tracking) theo id; khi có <paramref name="customerUserId"/> thì chỉ trả đơn thuộc khách đó.</summary>
    Task<Order?> GetOrderAsync(Guid orderId, Guid? customerUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Lần thu tiền <c>Pending</c> mới nhất của đơn (có tracking); null nếu chưa có.</summary>
    Task<Payment?> GetLatestPendingCollectionAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Thanh toán kèm Order (chỉ đọc) nếu thuộc đơn của khách; null nếu không tồn tại hoặc không thuộc khách.</summary>
    Task<Payment?> GetOwnedAsync(Guid paymentId, Guid orderId, Guid customerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thanh toán (chỉ đọc) có 16 ký tự hex đầu của id (bỏ dấu "-", chữ thường) trùng <paramref name="paymentIdPrefix"/>;
    /// null nếu không có.
    /// </summary>
    Task<Payment?> FindByIdPrefixAsync(string paymentIdPrefix, CancellationToken cancellationToken = default);

    /// <summary>Đã ghi nhận giao dịch của nhà cung cấp này chưa (chống xử lý webhook lặp).</summary>
    Task<bool> ProviderTransactionExistsAsync(string providerTransactionId, CancellationToken cancellationToken = default);

    /// <summary>Danh sách giao dịch thu / hoàn trang quản trị (lọc theo phạm vi người xem).</summary>
    Task<(IReadOnlyList<AdminPaymentRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminPaymentFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Tổng thu / hoàn thành công và số tiền đang chờ, cùng bộ lọc với <see cref="GetAdminPagedAsync"/>.</summary>
    Task<AdminPaymentStatsRow> GetAdminStatsAsync(AdminPaymentFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Id đơn của thanh toán (chỉ đọc, dùng để lấy khoá trước khi tải bản tracking); null nếu không có.</summary>
    Task<Guid?> GetOrderIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>Thanh toán (tracking) kèm Order và toàn bộ Payments của đơn; null nếu không có.</summary>
    Task<Payment?> GetForUpdateAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>Khoản hoàn này đã gắn hồ sơ hoàn tiền (refund case) chưa.</summary>
    Task<bool> HasRefundCaseAsync(Guid paymentId, CancellationToken cancellationToken = default);
}
