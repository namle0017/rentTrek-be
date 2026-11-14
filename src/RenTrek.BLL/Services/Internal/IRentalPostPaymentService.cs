using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Internal;

/// <summary>
/// Xử lý khoản thu sau khi đơn thuê đã thanh toán (gia hạn, phí vượt cọc) khi webhook xác nhận tiền vào.
/// Chạy trong transaction của webhook, không tự SaveChanges. Hợp đồng nội bộ BLL (nhận entity), không dùng ở API.
/// </summary>
public interface IRentalPostPaymentService
{
    Task ApplySucceededPaymentAsync(Payment payment, Order order, DateTimeOffset now, CancellationToken cancellationToken = default);
}
