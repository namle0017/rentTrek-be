using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Promotion.Internal;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ đánh giá khuyến mãi (hiệu lực, đối tượng áp dụng, giới hạn lượt dùng, chọn bậc và tính mức giảm).
/// </summary>
public class PromotionService : IPromotionService
{
    // Lượt dùng tính cả đơn đã áp dụng và đơn đang giữ chỗ
    private static readonly string[] CountedStatuses = [AppliedPromotionStatuses.Applied, AppliedPromotionStatuses.Reserved];

    private readonly IUnitOfWork _unitOfWork;

    public PromotionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PromotionEvaluationResult> EvaluateAsync(
        PromotionEvaluationInput request,
        CancellationToken cancellationToken = default)
    {
        var code = request.PromotionCode?.Trim();
        if (string.IsNullOrEmpty(code))
            return new PromotionEvaluationResult();

        var promotion = await _unitOfWork.Promotions.GetByCodeAsync(code, cancellationToken);
        if (promotion is null)
            return Fail(ErrorCodes.PromotionInvalid, "Mã khuyến mãi không tồn tại");

        if (!string.Equals(promotion.Status, PromotionStatuses.Active, StringComparison.OrdinalIgnoreCase))
            return Fail(ErrorCodes.PromotionInvalid, "Mã khuyến mãi hiện không còn hiệu lực", promotion.PromotionId);

        if (request.NowUtc < promotion.StartAt || request.NowUtc > promotion.EndAt)
            return Fail(ErrorCodes.PromotionInvalid, "Mã khuyến mãi chưa bắt đầu hoặc đã hết hạn", promotion.PromotionId);

        if (!AppliesToOrderType(promotion.AppliesTo, request.OrderType))
            return Fail(ErrorCodes.PromotionInvalid, "Mã khuyến mãi không áp dụng cho đơn thuê thiết bị", promotion.PromotionId);

        if (promotion.MaxUses is { } maxUses
            && await _unitOfWork.Promotions.CountUsageAsync(promotion.PromotionId, CountedStatuses, null, cancellationToken) >= maxUses)
        {
            return Fail(ErrorCodes.PromotionExhausted, "Mã khuyến mãi đã hết lượt sử dụng", promotion.PromotionId);
        }

        if (promotion.MaxUsesPerUser is { } maxPerUser
            && await _unitOfWork.Promotions.CountUsageAsync(
                promotion.PromotionId, CountedStatuses, request.CustomerUserId, cancellationToken) >= maxPerUser)
        {
            return Fail(ErrorCodes.PromotionExhausted, "Bạn đã dùng hết số lượt cho mã khuyến mãi này", promotion.PromotionId);
        }

        // Bậc quà tặng không được hỗ trợ ở luồng thuê: loại khỏi danh sách bậc
        var tiers = promotion.PromotionTiers.Select(tier => new PromotionTierOption
        {
            TierId = tier.PromotionTierId,
            MinThreshold = tier.MinThreshold,
            QualificationMetric = tier.QualificationMetric,
            DiscountType = tier.DiscountType,
            DiscountValue = tier.DiscountValue,
            IsGift = tier.GiftProductVariantId.HasValue || (tier.GiftQuantity ?? 0) > 0
        });

        var selectedTier = PromotionDiscountCalculator.SelectTier(
            tiers, request.SubtotalAmount, request.TotalQuantity, request.BillableDays);
        if (selectedTier is null)
            return Fail(ErrorCodes.PromotionInvalid, "Đơn hàng chưa đạt điều kiện áp dụng mã khuyến mãi", promotion.PromotionId);

        var isLineScope = PromotionRules.Matches(promotion.ApplicationScope, PromotionRules.ScopeLine);
        var calculation = PromotionDiscountCalculator.Calculate(
            selectedTier.DiscountType,
            selectedTier.DiscountValue,
            isLineScope,
            request.SubtotalAmount,
            request.Lines);

        // Mức giảm không vượt quá tạm tính
        var discount = Math.Min(calculation.DiscountAmount, request.SubtotalAmount);
        if (discount <= 0)
            return Fail(ErrorCodes.PromotionInvalid, "Mã khuyến mãi không tạo ra mức giảm cho đơn này", promotion.PromotionId);

        return new PromotionEvaluationResult
        {
            IsApplied = true,
            PromotionId = promotion.PromotionId,
            TierId = selectedTier.TierId,
            Code = promotion.PromotionCode,
            Name = promotion.Name,
            Description = promotion.Description,
            DiscountAmount = discount,
            IsLineScope = isLineScope,
            LineDiscounts = calculation.LineDiscounts
        };
    }

    public Task LockAsync(Guid promotionId, CancellationToken cancellationToken = default)
        => _unitOfWork.Promotions.AcquireLockAsync(promotionId, cancellationToken);

    private static bool AppliesToOrderType(string appliesTo, string orderType)
        => string.Equals(appliesTo?.Trim(), orderType, StringComparison.OrdinalIgnoreCase)
           || (orderType == OrderTypes.Rental && PromotionRules.Matches(appliesTo, PromotionRules.AppliesToRental));

    private static PromotionEvaluationResult Fail(string errorCode, string message, Guid? promotionId = null)
        => new()
        {
            PromotionId = promotionId,
            ErrorCode = errorCode,
            Message = message
        };
}
