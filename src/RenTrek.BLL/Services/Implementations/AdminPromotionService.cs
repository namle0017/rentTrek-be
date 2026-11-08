using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Chương trình khuyến mãi, bậc giảm giá và lịch sử sử dụng.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminPromotionService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminPromotionService
{
    private static AdminPromotionResponse ToResponse(AdminPromotionRow x) => new(
        x.PromotionId, x.Name, x.PromotionCode, x.Description, x.StartAt, x.EndAt, x.AppliesTo, x.ApplicationScope,
        x.UsedCount, x.MaxUses, x.MaxUsesPerUser, x.IsStackable, x.Priority, x.Status);

    public async Task<PagedResponse<AdminPromotionResponse>> GetPromotionsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        CheckPaging(request);
        var page = await _unitOfWork.Promotions.GetAdminPagedAsync(new AdminPromotionFilter
        {
            Status = request.Status,
            AppliesTo = request.AppliesTo,
            Search = request.Search,
            SortBy = request.SortBy,
            IsDescending = IsDescending(request),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
        return ToPaged(page, request, ToResponse);
    }

    public async Task<AdminPromotionDetailResponse> GetPromotionAsync(Guid id, CancellationToken cancellationToken)
    {
        var promotion = await _unitOfWork.Promotions.GetAdminRowAsync(id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khuyến mãi");
        var tiers = (await _unitOfWork.Promotions.GetTiersAsync(id, cancellationToken))
            .Select(x => new AdminPromotionTierResponse(x.PromotionTierId, x.QualificationMetric,
                x.MinThreshold, x.DiscountType, x.DiscountValue, x.GiftProductVariantId, x.GiftQuantity)).ToList();
        return new(ToResponse(promotion), tiers);
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
            if (code != null && await _unitOfWork.Promotions.CodeExistsAsync(code, promotionId, cancellationToken))
                throw new ConflictException("Mã khuyến mãi đã tồn tại");
            var promotion = id.HasValue
                ? await _unitOfWork.Promotions.GetForUpdateAsync(promotionId, cancellationToken) ?? throw new NotFoundException("Không tìm thấy khuyến mãi")
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
            var promotion = await _unitOfWork.Promotions.GetForUpdateAsync(id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy khuyến mãi");
            if (status == "Active")
            {
                Check(promotion.EndAt > Now, "status", "Khuyến mãi đã hết hạn");
                Check((await _unitOfWork.Promotions.GetTiersAsync(id, cancellationToken)).Count > 0, "status", "Cần cấu hình ít nhất một bậc ưu đãi");
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
            if (await _unitOfWork.Promotions.GetForUpdateAsync(id, cancellationToken) == null) throw new NotFoundException("Không tìm thấy khuyến mãi");
            var giftIds = tiers.Where(x => x.GiftProductVariantId.HasValue).Select(x => x.GiftProductVariantId!.Value).Distinct().ToList();
            Check(await _unitOfWork.Promotions.CountActiveVariantsAsync(giftIds, cancellationToken) == giftIds.Count, "giftProductVariantId", "Biến thể quà tặng không tồn tại");
            var old = await _unitOfWork.Promotions.GetTiersAsync(id, cancellationToken);
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

    public async Task<PagedResponse<AdminPromotionUsageResponse>> GetPromotionUsageAsync(Guid id, AdminQueryRequest request, CancellationToken cancellationToken)
    {
        CheckPaging(request);
        var page = await _unitOfWork.Promotions.GetUsagePagedAsync(new PromotionUsageFilter
        {
            PromotionId = id,
            SortBy = request.SortBy,
            IsDescending = IsDescending(request),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
        return ToPaged(page, request, x => new AdminPromotionUsageResponse(x.AppliedPromotionId, x.OrderId, x.OrderCode,
            x.DiscountAmount, x.GiftQuantity, x.Status, x.AppliedAt));
    }
}
