using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Chương trình khuyến mãi, bậc giảm giá và lịch sử sử dụng.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminPromotionService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminPromotionService
{
    private Expression<Func<Promotion, AdminPromotionResponse>> PromotionProjection => promotion => new(
        promotion.PromotionId, promotion.Name, promotion.PromotionCode, promotion.Description, promotion.StartAt,
        promotion.EndAt, promotion.AppliesTo, promotion.ApplicationScope,
        promotion.PromotionTiers.SelectMany(t => t.AppliedPromotions).Where(a => a.Status == AppliedPromotionStatuses.Applied || a.Status == AppliedPromotionStatuses.Reserved).Select(a => a.OrderId).Distinct().Count(),
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

    public Task<Guid> CreatePromotionAsync(CreatePromotionRequest request, CancellationToken cancellationToken) => SavePromotionAsync(null, request, cancellationToken);

    public Task<Guid> UpdatePromotionAsync(Guid id, UpdatePromotionRequest request, CancellationToken cancellationToken) => SavePromotionAsync(id, request, cancellationToken);

    private async Task<Guid> SavePromotionAsync(Guid? id, CreatePromotionRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var name = request.Name.Trim();
        var code = string.IsNullOrWhiteSpace(request.PromotionCode) ? null : request.PromotionCode.Trim().ToUpperInvariant();
        var promotionId = id ?? Guid.NewGuid();
        await AtomicAsync("promotions", async () =>
        {
            if (code != null && await _unitOfWork.Promotions.Query().AnyAsync(x => x.PromotionId != promotionId && x.PromotionCode == code, cancellationToken))
                throw new ConflictException("Mã khuyến mãi đã tồn tại");
            var promotion = id.HasValue
                ? await _unitOfWork.Promotions.QueryTracking().SingleOrDefaultAsync(x => x.PromotionId == promotionId && !x.IsDeleted, cancellationToken) ?? throw new NotFoundException("Không tìm thấy khuyến mãi")
                : new Promotion { PromotionId = promotionId, CreatedByUserId = ActorId, CreatedAt = Now, Status = PromotionStatuses.Draft };
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
        RequireAdmin(); var status = request.Status;
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
        RequireAdmin();
        var tiers = request.Tiers;
        await AtomicAsync("promotions", async () =>
        {
            if (!await _unitOfWork.Promotions.Query().AnyAsync(x => x.PromotionId == id && !x.IsDeleted, cancellationToken)) throw new NotFoundException("Không tìm thấy khuyến mãi");
            var giftIds = tiers.Where(x => x.GiftProductVariantId.HasValue).Select(x => x.GiftProductVariantId!.Value).Distinct().ToList();
            Check(await _unitOfWork.ProductVariants.Query().CountAsync(x => giftIds.Contains(x.Id) && !x.IsDeleted, cancellationToken) == giftIds.Count, "giftProductVariantId", "Biến thể quà tặng không tồn tại");
            var old = await _unitOfWork.PromotionTiers.QueryTracking().Where(x => x.PromotionId == id && !x.IsDeleted).ToListAsync(cancellationToken);
            foreach (var tier in old) { tier.IsDeleted = true; tier.UpdatedAt = Now; }
            foreach (var tier in tiers)
                await _unitOfWork.PromotionTiers.AddAsync(new PromotionTier
                {
                    PromotionTierId = Guid.NewGuid(),
                    PromotionId = id,
                    QualificationMetric = tier.QualificationMetric,
                    MinThreshold = tier.MinThreshold,
                    DiscountType = tier.DiscountType,
                    DiscountValue = tier.DiscountValue,
                    GiftProductVariantId = tier.GiftProductVariantId,
                    GiftQuantity = tier.GiftQuantity,
                    CreatedAt = Now,
                    UpdatedAt = Now
                }, cancellationToken);
            await AuditAsync("Update", "Promotion", id, new { tiers }, cancellationToken);
        }, cancellationToken);
    }

    public Task<PagedResponse<AdminPromotionUsageResponse>> GetPromotionUsageAsync(Guid id, AdminQueryRequest request, CancellationToken cancellationToken)
        => PageAsync(_unitOfWork.AppliedPromotions.Query().Where(x => x.PromotionTier.PromotionId == id), request,
            x => new AdminPromotionUsageResponse(x.AppliedPromotionId, x.OrderId, x.Order.OrderCode, x.DiscountAmount,
                x.GiftQuantity, x.Status, x.AppliedAt), cancellationToken, "AppliedAt");
}
