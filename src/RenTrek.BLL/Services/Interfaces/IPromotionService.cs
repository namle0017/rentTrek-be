using RenTrek.BLL.DTOs.Promotion.Internal;
using RenTrek.BLL.DTOs.Promotion.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IPromotionService
{
    /// <summary>
    /// Đánh giá mã khuyến mãi cho một đơn. Không ném exception khi mã không áp được: lý do trả trong
    /// <see cref="PromotionEvaluationResult.ErrorCode"/>; mã trống nghĩa là không dùng khuyến mãi (không áp, không lỗi).
    /// </summary>
    Task<PromotionEvaluationResult> EvaluateAsync(PromotionEvaluationInput request, CancellationToken cancellationToken = default);

    /// <summary>Khoá advisory theo transaction cho khuyến mãi (gọi trong transaction đã mở, trước khi đánh giá lại lượt dùng).</summary>
    Task LockAsync(Guid promotionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mã khuyến mãi khách có thể nhập cho đơn thuê lúc <paramref name="nowUtc"/>: đang Active, trong hạn,
    /// áp dụng cho đơn thuê, còn lượt (tổng) và có ít nhất một bậc giảm tiền. Không xét lượt theo từng khách.
    /// </summary>
    Task<IReadOnlyList<PublicPromotionResponse>> GetAvailableAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
