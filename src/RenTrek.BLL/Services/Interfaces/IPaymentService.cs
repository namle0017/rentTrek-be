using RenTrek.BLL.DTOs.Payment.Requests;
using RenTrek.BLL.DTOs.Payment.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Tạo hướng dẫn chuyển khoản cho toàn bộ số phải trả của đơn. Đã có lần <c>Pending</c> thì trả lại lần đó
    /// (<c>Created = false</c>), không tạo mới.
    /// </summary>
    Task<(PaymentResponse Payment, bool Created)> CreateAsync(
        Guid userId,
        Guid orderId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentStatusResponse> GetStatusAsync(
        Guid userId,
        Guid orderId,
        Guid paymentId,
        CancellationToken cancellationToken = default);


}
