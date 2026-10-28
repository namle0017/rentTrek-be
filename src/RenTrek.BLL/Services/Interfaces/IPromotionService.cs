using RenTrek.BLL.DTOs.Promotion;

namespace RenTrek.BLL.Services.Interfaces;

public interface IPromotionService
{
    /// <summary>
    /// Đánh giá mã khuyến mãi cho một đơn. Không ném exception khi mã không áp được: lý do trả trong
    /// <see cref="EvaluatePromotionResponse.ErrorCode"/>; mã trống nghĩa là không dùng khuyến mãi (không áp, không lỗi).
    /// </summary>
    Task<EvaluatePromotionResponse> EvaluateAsync(EvaluatePromotionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Khoá advisory theo transaction cho khuyến mãi (gọi trong transaction đã mở, trước khi đánh giá lại lượt dùng).</summary>
    Task LockAsync(Guid promotionId, CancellationToken cancellationToken = default);
}
