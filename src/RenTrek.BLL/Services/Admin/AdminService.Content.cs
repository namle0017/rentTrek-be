using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Admin;

public sealed partial class AdminService
{
    private Expression<Func<Promotion, AdminPromotionResponse>> PromotionProjection => promotion => new(
        promotion.PromotionId, promotion.Name, promotion.PromotionCode, promotion.Description, promotion.StartAt,
        promotion.EndAt, promotion.AppliesTo, promotion.ApplicationScope,
        promotion.PromotionTiers.SelectMany(t => t.AppliedPromotions).Where(a => a.Status == "Applied" || a.Status == "Reserved").Select(a => a.OrderId).Distinct().Count(),
        promotion.MaxUses, promotion.MaxUsesPerUser, promotion.IsStackable, promotion.Priority, promotion.Status);

    public Task<PagedResponse<AdminPromotionResponse>> GetPromotionsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Promotions.Query().Where(x => !x.IsDeleted);
        if (request.Status != null) query = query.Where(x => x.Status == request.Status);
        if (request.AppliesTo != null) query = query.Where(x => x.AppliesTo == request.AppliesTo);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(search) || (x.PromotionCode != null && x.PromotionCode.ToLower().Contains(search)));
        }
        return PageAsync(query, request, PromotionProjection, cancellationToken, "CreatedAt", "Name", "StartAt", "EndAt", "Priority");
    }

    public async Task<AdminPromotionDetailResponse> GetPromotionAsync(Guid id, CancellationToken cancellationToken)
    {
        var promotion = await _unitOfWork.Promotions.Query().Where(x => x.PromotionId == id && !x.IsDeleted)
            .Select(PromotionProjection).SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Không tìm thấy khuyến mãi");
        var tiers = await _unitOfWork.PromotionTiers.Query().Where(x => x.PromotionId == id && !x.IsDeleted)
            .OrderBy(x => x.MinThreshold).Select(x => new AdminPromotionTierResponse(x.PromotionTierId, x.QualificationMetric,
                x.MinThreshold, x.DiscountType, x.DiscountValue, x.GiftProductVariantId, x.GiftQuantity)).ToListAsync(cancellationToken);
        return new(promotion, tiers);
    }

    public async Task<Guid> SavePromotionAsync(Guid? id, PromotionWriteRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var name = Required(request.Name, "name", 200);
        var code = string.IsNullOrWhiteSpace(request.PromotionCode) ? null : Required(request.PromotionCode, "promotionCode", 50).ToUpperInvariant();
        Check(request.StartAt.Kind == DateTimeKind.Utc && request.EndAt.Kind == DateTimeKind.Utc && request.EndAt > request.StartAt, "endAt", "Ngày kết thúc phải sau ngày bắt đầu, dùng giờ UTC");
        Choice(request.AppliesTo, "appliesTo", "Tour", "Rental", "All");
        Choice(request.ApplicationScope, "applicationScope", "Order", "Line");
        Check(request.MaxUses == null || request.MaxUses > 0, "maxUses", "Giới hạn phải lớn hơn 0");
        Check(request.MaxUsesPerUser == null || request.MaxUsesPerUser > 0, "maxUsesPerUser", "Giới hạn phải lớn hơn 0");
        Check(request.Priority >= 0, "priority", "Độ ưu tiên không được âm");
        var promotionId = id ?? Guid.NewGuid();
        await AtomicAsync("promotions", async () =>
        {
            if (code != null && await _unitOfWork.Promotions.Query().AnyAsync(x => x.PromotionId != promotionId && x.PromotionCode == code, cancellationToken))
                throw new ConflictException("Mã khuyến mãi đã tồn tại");
            var promotion = id.HasValue
                ? await _unitOfWork.Promotions.QueryTracking().SingleOrDefaultAsync(x => x.PromotionId == promotionId && !x.IsDeleted, cancellationToken) ?? throw new NotFoundException("Không tìm thấy khuyến mãi")
                : new Promotion { PromotionId = promotionId, CreatedByUserId = ActorId, CreatedAt = Now, Status = "Draft" };
            var before = new { promotion.Name, promotion.Status, promotion.PromotionCode };
            promotion.Name = name; promotion.PromotionCode = code; promotion.Description = request.Description?.Trim();
            promotion.StartAt = request.StartAt; promotion.EndAt = request.EndAt; promotion.AppliesTo = request.AppliesTo;
            promotion.ApplicationScope = request.ApplicationScope; promotion.MaxUses = request.MaxUses;
            promotion.MaxUsesPerUser = request.MaxUsesPerUser; promotion.IsStackable = request.IsStackable;
            promotion.Priority = request.Priority; promotion.UpdatedAt = Now;
            if (!id.HasValue) await _unitOfWork.Promotions.AddAsync(promotion, cancellationToken);
            await AuditAsync(id.HasValue ? "Update" : "Create", "Promotion", promotionId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
        return promotionId;
    }

    public async Task SetPromotionStatusAsync(Guid id, StatusRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var status = Choice(request.Status, "status", "Active", "Paused", "Expired");
        await AtomicAsync("promotions", async () =>
        {
            var promotion = await _unitOfWork.Promotions.QueryTracking().SingleOrDefaultAsync(x => x.PromotionId == id && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy khuyến mãi");
            if (status == "Active")
            {
                Check(promotion.EndAt > Now, "status", "Khuyến mãi đã hết hạn");
                Check(await _unitOfWork.PromotionTiers.Query().AnyAsync(x => x.PromotionId == id && !x.IsDeleted, cancellationToken), "status", "Cần cấu hình ít nhất một bậc ưu đãi");
            }
            var before = promotion.Status; promotion.Status = status; promotion.UpdatedAt = Now;
            await AuditAsync("Update", "Promotion", id, new { before, after = status }, cancellationToken);
        }, cancellationToken);
    }

    public async Task SetPromotionTiersAsync(Guid id, PromotionTiersRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); Check(request.Tiers is { Count: > 0 and <= 50 }, "tiers", "Cần từ 1 đến 50 bậc ưu đãi");
        var tiers = request.Tiers!;
        Check(tiers.Select(x => (x.QualificationMetric, x.MinThreshold)).Distinct().Count() == tiers.Count, "tiers", "Không được trùng điều kiện ưu đãi");
        foreach (var tier in tiers)
        {
            Choice(tier.QualificationMetric, "qualificationMetric", "OrderAmount", "ParticipantCount", "RentalDays");
            Choice(tier.DiscountType, "discountType", "Percentage", "FixedAmount", "GiftItem");
            Check(tier.MinThreshold >= 0 && (tier.QualificationMetric == "OrderAmount" || tier.MinThreshold == decimal.Truncate(tier.MinThreshold)), "minThreshold", "Điều kiện phải là số không âm hợp lệ");
            Check(tier.DiscountValue >= 0 && (tier.DiscountType != "Percentage" || tier.DiscountValue <= 100), "discountValue", "Giá trị giảm không hợp lệ");
            if (tier.DiscountType == "GiftItem")
                Check(tier.GiftProductVariantId.HasValue && tier.GiftQuantity > 0, "giftProductVariantId", "Quà tặng cần biến thể và số lượng dương");
            else Check(tier.GiftProductVariantId == null && tier.GiftQuantity == null, "giftProductVariantId", "Chỉ cấu hình quà tặng cho bậc GiftItem");
        }
        await AtomicAsync("promotions", async () =>
        {
            if (!await _unitOfWork.Promotions.Query().AnyAsync(x => x.PromotionId == id && !x.IsDeleted, cancellationToken)) throw new NotFoundException("Không tìm thấy khuyến mãi");
            var giftIds = tiers.Where(x => x.GiftProductVariantId.HasValue).Select(x => x.GiftProductVariantId!.Value).Distinct().ToList();
            Check(await _unitOfWork.ProductVariants.Query().CountAsync(x => giftIds.Contains(x.Id) && !x.IsDeleted, cancellationToken) == giftIds.Count, "giftProductVariantId", "Biến thể quà tặng không tồn tại");
            var old = await _unitOfWork.PromotionTiers.QueryTracking().Where(x => x.PromotionId == id && !x.IsDeleted).ToListAsync(cancellationToken);
            foreach (var tier in old) { tier.IsDeleted = true; tier.UpdatedAt = Now; }
            foreach (var tier in tiers)
                await _unitOfWork.PromotionTiers.AddAsync(new PromotionTier { PromotionTierId = Guid.NewGuid(), PromotionId = id,
                    QualificationMetric = tier.QualificationMetric, MinThreshold = tier.MinThreshold, DiscountType = tier.DiscountType,
                    DiscountValue = tier.DiscountValue, GiftProductVariantId = tier.GiftProductVariantId, GiftQuantity = tier.GiftQuantity, CreatedAt = Now, UpdatedAt = Now }, cancellationToken);
            await AuditAsync("Update", "Promotion", id, new { tiers }, cancellationToken);
        }, cancellationToken);
    }

    public Task<PagedResponse<AdminPromotionUsageResponse>> GetPromotionUsageAsync(Guid id, AdminQueryRequest request, CancellationToken cancellationToken)
        => PageAsync(_unitOfWork.AppliedPromotions.Query().Where(x => x.PromotionTier.PromotionId == id), request,
            x => new AdminPromotionUsageResponse(x.AppliedPromotionId, x.OrderId, x.Order.OrderCode, x.DiscountAmount,
                x.GiftQuantity, x.Status, x.AppliedAt), cancellationToken, "AppliedAt");
}
