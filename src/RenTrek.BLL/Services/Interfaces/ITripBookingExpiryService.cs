namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Đóng đơn chuyến đi quá hạn giữ chỗ mà chưa có tiền về: đơn sang Expired, huỷ khoản thu đang chờ, huỷ participant,
/// nhả khuyến mãi và thu hồi lời mời. Idempotent; không đụng đơn đã có tiền (NeedsReconciliation).
/// </summary>
public interface ITripBookingExpiryService
{
    /// <summary>Gom một lô đơn quá hạn (job gọi rồi xử lý từng đơn trong scope riêng).</summary>
    Task<IReadOnlyList<Guid>> CollectExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Trả true nếu đơn vừa được chuyển sang Expired; false nếu đơn không còn đủ điều kiện.</summary>
    Task<bool> ExpireAsync(Guid orderId, CancellationToken cancellationToken = default);
}
