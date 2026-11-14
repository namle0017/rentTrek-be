using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Huỷ đơn thuê đã thanh toán và xử lý khách không đến nhận (no-show).</summary>
public interface IRentalCancellationService
{
    Task<RentalCancellationPreviewResponse> PreviewAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default);

    Task<RentalOrderDetailResponse> CancelPaidAsync(Guid userId, Guid orderId, CancelPaidRentalOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đóng đơn no-show: mất trọn cọc, nhả giữ chỗ, mở hồ sơ hoàn phần còn lại (chờ khách nhập STK), báo cáo cho admin.
    /// <paramref name="actorUserId"/> null = job hệ thống. Trả false khi đơn chưa tới mốc / đã xử lý (job bỏ qua).
    /// </summary>
    Task<bool> ProcessNoShowAsync(Guid orderId, Guid? actorUserId, string? note, bool throwIfNotEligible, CancellationToken cancellationToken = default);
}
