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

    /// <summary>
    /// Xử lý webhook SePay: xác thực API key, khớp nội dung chuyển khoản với lần thanh toán, chống xử lý lặp,
    /// rồi xác nhận đơn hoặc đánh dấu cần đối soát.
    /// </summary>
    Task HandleSePayWebhookAsync(
        string? authorization,
        SePayWebhookRequest request,
        CancellationToken cancellationToken = default);
}
