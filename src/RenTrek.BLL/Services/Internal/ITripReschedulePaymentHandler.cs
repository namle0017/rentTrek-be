using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Internal;

/// <summary>Webhook gọi khi khoản chênh lệch đổi lịch đã trả.</summary>
public interface ITripReschedulePaymentHandler
{
    /// <summary>Chạy trong transaction + khoá đơn của caller, không tự commit.</summary>
    Task ApplySucceededPaymentAsync(Payment payment, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
