using RenTrek.BLL.DTOs.RentalOrder;

namespace RenTrek.BLL.Services.Interfaces;

public interface IRentalOrderService
{
    /// <summary>
    /// Báo giá đơn thuê: tính giá, khả dụng, khuyến mãi và chính sách hoàn tiền. Không lưu dữ liệu, không giữ tài sản.
    /// Trả 200 kể cả khi thiếu hàng hoặc mã khuyến mãi không áp được.
    /// </summary>
    Task<RentalQuoteResponse> QuoteAsync(Guid userId, RentalQuoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo đơn thuê và giữ tài sản (Reserved) một cách nguyên tử. Gửi lại cùng <paramref name="idempotencyKey"/> với cùng nội dung
    /// trả lại đơn đã tạo; khác nội dung thì 409 IDEMPOTENCY_KEY_REUSED.
    /// </summary>
    Task<RentalOrderDetailResponse> CreateAsync(
        Guid userId,
        CreateRentalOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết đơn thuê của khách; 404 ORDER_NOT_FOUND nếu không tồn tại hoặc không thuộc khách.</summary>
    Task<RentalOrderDetailResponse> GetByIdAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default);
}
