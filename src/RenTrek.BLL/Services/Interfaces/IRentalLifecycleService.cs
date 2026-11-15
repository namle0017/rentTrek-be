using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Vòng đời đơn thuê sau thanh toán: phí trễ nhận theo bậc, no-show, nhắc trả, phạt trả trễ, nghi mất,
/// tự xác nhận đã nhận hàng, nhắc nhập STK, huỷ gia hạn chưa thanh toán. Mỗi việc idempotent (chỉ nâng bậc, khử trùng thông báo).
/// </summary>
public interface IRentalLifecycleService
{
    /// <summary>Gom việc cần làm ở thời điểm hiện tại (job gọi rồi xử lý từng việc trong scope riêng).</summary>
    Task<IReadOnlyList<RentalLifecycleWorkItem>> CollectWorkAsync(CancellationToken cancellationToken = default);

    Task ProcessAsync(RentalLifecycleWorkItem item, CancellationToken cancellationToken = default);

    /// <summary>Khách xác nhận đã nhận đồ và kiểm tra kỹ.</summary>
    Task<RentalOrderDetailResponse> ConfirmReceiptAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default);
}
