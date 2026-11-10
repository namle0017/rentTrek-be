using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Responses;

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

    /// <summary>
    /// Báo giá gia hạn đơn thuê (không ghi dữ liệu): kiểm đơn được gia hạn, kiểm xung đột tài sản trong [RentalEndAt, newEndAt)
    /// và tính tiền thêm theo bậc giá của tổng số ngày mới. 404 nếu không thuộc khách; 409 RENTAL_NOT_EXTENDABLE / RESOURCE_UNAVAILABLE.
    /// </summary>
    Task<RentalExtensionQuoteResponse> QuoteExtensionAsync(
        Guid userId,
        Guid orderId,
        ExtendRentalQuoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Áp dụng gia hạn trong transaction có khoá đơn và khoá biến thể: tính lại báo giá trong khoá, chỉ áp dụng khi
    /// acceptedExtraAmount khớp (ngược lại 409 RENTAL_EXTENSION_PRICE_CHANGED), ghi lịch sử điều chỉnh và phí Extension nếu tiền thêm lớn hơn 0.
    /// </summary>
    Task<RentalExtensionResultResponse> ExtendAsync(
        Guid userId,
        Guid orderId,
        ExtendRentalOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lịch sử điều chỉnh của đơn thuê thuộc khách (cũ nhất trước); 404 ORDER_NOT_FOUND nếu không thuộc khách.</summary>
    Task<List<RentalOrderAdjustmentResponse>> GetAdjustmentsAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    /// <summary>Hủy đơn thuê chưa thanh toán của khách: trả thiết bị Reserved, hoàn khuyến mãi, hủy payment Pending. 409 ORDER_NOT_CANCELLABLE nếu không đủ điều kiện.</summary>
    Task<RentalOrderDetailResponse> CancelUnpaidAsync(Guid userId, Guid orderId, CancelRentalOrderRequest request, CancellationToken cancellationToken = default);
}
