using RenTrek.DAL.Entities;

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
}
