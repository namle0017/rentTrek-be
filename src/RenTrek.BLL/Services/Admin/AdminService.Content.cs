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

    private static readonly Expression<Func<Post, AdminPostResponse>> PostProjection = post => new(post.PostId, post.Title,
        post.Slug, post.PostType, post.CreatedByUser.FullName, post.Status, post.PublishedAt,
        post.TourPosts.Count(x => !x.Tour.IsDeleted), post.ParticipantPostReads.Count);

    public Task<PagedResponse<AdminPostResponse>> GetPostsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Posts.Query().Where(x => !x.IsDeleted);
        if (request.Status != null) query = query.Where(x => x.Status == request.Status);
        if (request.PostType != null) query = query.Where(x => x.PostType == request.PostType);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim().ToLower(); query = query.Where(x => x.Title.ToLower().Contains(search) || x.Slug.Contains(search)); }
        return PageAsync(query, request, PostProjection, cancellationToken, "CreatedAt", "Title", "PublishedAt");
    }

    public async Task<AdminPostDetailResponse> GetPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await _unitOfWork.Posts.Query().Where(x => x.PostId == id && !x.IsDeleted)
            .Select(PostProjection).SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Không tìm thấy bài viết");
        var body = await _unitOfWork.Posts.Query().Where(x => x.PostId == id).Select(x => new { x.Content,
            TagIds = x.PostTags.Select(t => t.TagId).ToList(), SoftSkillIds = x.PostSoftSkills.Select(s => s.SoftSkillId).ToList() }).SingleAsync(cancellationToken);
        return new(post, body.Content, body.TagIds, body.SoftSkillIds);
    }

    public async Task<Guid> SavePostAsync(Guid? id, PostWriteRequest request, CancellationToken cancellationToken)
    {
        var title = Required(request.Title, "title", 250); var slug = Required(request.Slug, "slug", 250);
        Check(System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"), "slug", "Slug không hợp lệ");
        Choice(request.PostType, "postType", "Guide", "Skill", "Location", "News");
        var content = new Ganss.Xss.HtmlSanitizer().Sanitize(Required(request.Content, "content", 200000));
        Check(!string.IsNullOrWhiteSpace(content), "content", "Nội dung bài viết không được rỗng");
        Check(request.TagIds != null && request.SoftSkillIds != null, "tagIds", "Danh sách tag và kỹ năng là bắt buộc");
        var tags = request.TagIds!.Distinct().ToList(); var skills = request.SoftSkillIds!.Distinct().ToList();
        var postId = id ?? Guid.NewGuid();
        await AtomicAsync("posts", async () =>
        {
            if (await _unitOfWork.Posts.Query().AnyAsync(x => x.PostId != postId && x.Slug == slug, cancellationToken)) throw new ConflictException("Slug đã được sử dụng");
            Check(await _unitOfWork.Tags.Query().CountAsync(x => tags.Contains(x.TagId) && !x.IsDeleted, cancellationToken) == tags.Count, "tagIds", "Tag không tồn tại");
            Check(await _unitOfWork.SoftSkills.Query().CountAsync(x => skills.Contains(x.SoftSkillId) && !x.IsDeleted, cancellationToken) == skills.Count, "softSkillIds", "Kỹ năng không tồn tại");
            var post = id.HasValue ? await _unitOfWork.Posts.QueryTracking().Include(x => x.PostTags).Include(x => x.PostSoftSkills)
                .SingleOrDefaultAsync(x => x.PostId == postId && !x.IsDeleted, cancellationToken) ?? throw new NotFoundException("Không tìm thấy bài viết")
                : new Post { PostId = postId, CreatedByUserId = ActorId, Status = "Draft", CreatedAt = Now };
            var before = new { post.Title, post.Slug, post.Content };
            post.Title = title; post.Slug = slug; post.PostType = request.PostType; post.Content = content; post.UpdatedAt = Now;
            foreach (var tag in post.PostTags.Where(x => !tags.Contains(x.TagId)).ToList()) _unitOfWork.PostTags.Remove(tag);
            foreach (var skill in post.PostSoftSkills.Where(x => !skills.Contains(x.SoftSkillId)).ToList()) _unitOfWork.PostSoftSkills.Remove(skill);
            foreach (var tag in tags.Where(tag => !post.PostTags.Any(x => x.TagId == tag))) post.PostTags.Add(new PostTag { PostTagId = Guid.NewGuid(), PostId = postId, TagId = tag, CreatedAt = Now });
            foreach (var skill in skills.Where(skill => !post.PostSoftSkills.Any(x => x.SoftSkillId == skill))) post.PostSoftSkills.Add(new PostSoftSkill { PostSoftSkillId = Guid.NewGuid(), PostId = postId, SoftSkillId = skill, CreatedAt = Now });
            if (!id.HasValue) await _unitOfWork.Posts.AddAsync(post, cancellationToken);
            await AuditAsync(id.HasValue ? "Update" : "Create", "Post", postId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
        return postId;
    }

    public async Task SetPostStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        if (status == "Deleted") RequireAdmin();
        Choice(status, "status", "Published", "Archived", "Deleted");
        await AtomicAsync("posts", async () =>
        {
            var post = await _unitOfWork.Posts.QueryTracking().SingleOrDefaultAsync(x => x.PostId == id && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy bài viết");
            var before = post.Status;
            if (status == "Deleted") post.IsDeleted = true;
            else { post.Status = status; if (status == "Published") post.PublishedAt ??= Now; }
            post.UpdatedAt = Now;
            await AuditAsync(status == "Deleted" ? "Delete" : "Update", "Post", id, new { before, after = status }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<AdminReadAnalyticsResponse> GetReadAnalyticsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await _unitOfWork.Posts.Query().AnyAsync(x => x.PostId == id && !x.IsDeleted, cancellationToken)) throw new NotFoundException("Không tìm thấy bài viết");
        var tourIds = _unitOfWork.TourPosts.Query().Where(x => x.PostId == id).Select(x => x.TourId);
        var participants = _unitOfWork.BookingParticipants.Query().Where(x => !x.IsDeleted && x.Status != "Cancelled" && tourIds.Contains(x.Trip.TourId));
        var total = await participants.CountAsync(cancellationToken);
        var read = await participants.CountAsync(x => x.ParticipantPostReads.Any(r => r.PostId == id), cancellationToken);
        return new(id, total, read, total == 0 ? 0 : Math.Round(100m * read / total, 2));
    }
}
