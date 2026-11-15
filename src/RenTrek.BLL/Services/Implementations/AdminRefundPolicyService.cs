using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Chính sách hoàn tiền có phiên bản: bản đã có chuyến/đơn dùng thì bất biến, muốn đổi phải clone phiên bản mới (Draft)
/// rồi kích hoạt; kích hoạt đóng các bản cũ đang hiệu lực.
/// </summary>
public sealed class AdminRefundPolicyService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminRefundPolicyService
{
    private const string LockKey = "refund-policies";
    private const string EntityType = "RefundPolicy";

    public async Task<PagedResponse<RefundPolicyListItemResponse>> GetPoliciesAsync(
        RefundPolicyQueryRequest request, CancellationToken cancellationToken)
    {
        var now = Now;
        var page = await UnitOfWork.RefundPolicies.GetAdminPagedAsync(new AdminRefundPolicyFilter
        {
            Search = request.Search,
            PolicyType = string.IsNullOrWhiteSpace(request.PolicyType) ? null : request.PolicyType.Trim(),
            DisplayStatus = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim(),
            LatestOnly = request.LatestOnly,
            NowUtc = now,
            SortBy = request.SortBy,
            IsDescending = request.IsDescending,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
        return new PagedResponse<RefundPolicyListItemResponse>
        {
            Items = page.Items.Select(x => FillListItem(new RefundPolicyListItemResponse(), x, now)).ToList(),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalItems = page.TotalCount
        };
    }

    public async Task<RefundPolicyDetailResponse> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var now = Now;
        var policy = await UnitOfWork.RefundPolicies.GetDetailAsync(policyId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chính sách hoàn tiền", ErrorCodes.NotFound);
        var row = await UnitOfWork.RefundPolicies.GetAdminRowAsync(policyId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chính sách hoàn tiền", ErrorCodes.NotFound);
        var versions = await UnitOfWork.RefundPolicies.GetFamilyVersionsAsync(policy.PolicyFamilyId, cancellationToken);

        var detail = FillListItem(new RefundPolicyDetailResponse(), row, now);
        var locked = row.UsageCount > 0;
        var isLastActive = await IsLastActiveAsync(policy, now, cancellationToken);
        var isLatest = versions.Count == 0 || versions[0].Version == policy.Version;

        detail.UpdatedAt = policy.UpdatedAt;
        detail.IsLocked = locked;
        detail.CanEdit = IsAdmin && !locked;
        detail.CanDelete = IsAdmin && !locked && !isLastActive;
        detail.CanArchive = IsAdmin && policy.Status == RefundPolicyStatuses.Active && !isLastActive;
        detail.CanActivate = IsAdmin && (
            (policy.Status == RefundPolicyStatuses.Draft && policy.RefundPolicyRules.Count > 0)
            || (policy.Status == RefundPolicyStatuses.Archived && isLatest && policy.RefundPolicyRules.Count > 0));
        detail.CanClone = IsAdmin;
        detail.Rules = policy.RefundPolicyRules.Select(r => new RefundPolicyRuleResponse
        {
            RuleId = r.RefundPolicyRuleId,
            MinHoursBefore = r.MinHoursBefore,
            RefundPercentage = r.RefundPercentage,
            Description = $"Hủy trước ≥ {r.MinHoursBefore} giờ: hoàn {r.RefundPercentage:0.##}%"
        }).ToList();
        detail.Versions = versions.Select(v => new RefundPolicyVersionResponse
        {
            PolicyId = v.RefundPolicyId,
            Version = v.Version,
            DisplayStatus = RefundPolicyStatuses.ToDisplay(v.Status, v.EffectiveFrom, v.EffectiveUntil, now),
            EffectiveFrom = DateOnly.FromDateTime(v.EffectiveFrom),
            EffectiveUntil = v.EffectiveUntil.HasValue ? DateOnly.FromDateTime(v.EffectiveUntil.Value) : null,
            UsageCount = v.UsageCount
        }).ToList();
        return detail;
    }

    public async Task<Guid> CreatePolicyAsync(SaveRefundPolicyRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var policyId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var activate = !request.SaveAsDraft;
            if (activate) Check(request.Rules.Count > 0, "rules", "Cần ít nhất một quy tắc hoàn tiền để áp dụng");

            var policy = new RefundPolicy
            {
                RefundPolicyId = policyId,
                PolicyFamilyId = Guid.NewGuid(),
                Version = 1,
                CreatedByUserId = ActorId,
                Name = request.Name.Trim(),
                Description = NormalizeDescription(request.Description),
                PolicyType = request.PolicyType,
                Status = activate ? RefundPolicyStatuses.Active : RefundPolicyStatuses.Draft,
                EffectiveFrom = ToUtc(request.EffectiveFrom),
                EffectiveUntil = request.EffectiveUntil.HasValue ? ToUtc(request.EffectiveUntil.Value) : null,
                CreatedAt = Now,
                UpdatedAt = Now
            };
            if (activate) NormalizeEffectiveFrom(policy);
            await UnitOfWork.RefundPolicies.AddAsync(policy, cancellationToken);
            await ReplaceRulesAsync(policyId, request.Rules.Select(r => (r.MinHoursBefore, r.RefundPercentage)), cancellationToken);
            if (activate) await ApplyActivationEffectsAsync(policy, cancellationToken);
            await AuditAsync("Create", EntityType, policyId, request, cancellationToken);
        }, cancellationToken);
        return policyId;
    }

    public async Task UpdatePolicyAsync(Guid policyId, SaveRefundPolicyRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var policy = await GetEditableAsync(policyId, cancellationToken);
            Check(request.PolicyType == policy.PolicyType, "policyType", "Không thể đổi loại chính sách sau khi tạo");
            var before = new { policy.Name, policy.Description, policy.EffectiveFrom, policy.EffectiveUntil };
            var wasLastActive = await IsLastActiveAsync(policy, Now, cancellationToken);
            policy.Name = request.Name.Trim();
            policy.Description = NormalizeDescription(request.Description);
            policy.EffectiveFrom = ToUtc(request.EffectiveFrom);
            policy.EffectiveUntil = request.EffectiveUntil.HasValue ? ToUtc(request.EffectiveUntil.Value) : null;
            policy.UpdatedAt = Now;

            if (policy.Status == RefundPolicyStatuses.Active)
            {
                NormalizeEffectiveFrom(policy);
                // Không để đổi ngày làm mất chính sách hiệu lực duy nhất của loại này.
                if (wasLastActive && RefundPolicyStatuses.ToDisplay(policy.Status, policy.EffectiveFrom,
                        policy.EffectiveUntil, Now) != RefundPolicyStatuses.Active)
                    throw new ConflictException(
                        $"Đây là chính sách {policy.PolicyType} duy nhất đang hiệu lực; không thể đổi ngày khiến nó hết hiệu lực.",
                        ErrorCodes.RefundPolicyLastActive);
                await ApplyActivationEffectsAsync(policy, cancellationToken);
            }
            await ReplaceRulesAsync(policyId, request.Rules.Select(r => (r.MinHoursBefore, r.RefundPercentage)), cancellationToken);
            await AuditAsync("Update", EntityType, policyId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
    }

    public async Task SetRulesAsync(Guid policyId, RefundPolicyRulesRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var policy = await GetEditableAsync(policyId, cancellationToken);
            await ReplaceRulesAsync(policy.RefundPolicyId,
                request.Rules.Select(r => (r.MinHoursBefore, r.RefundPercentage)), cancellationToken);
            policy.UpdatedAt = Now;
            await AuditAsync("Update", EntityType, policyId, new { rules = request.Rules }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Guid> CloneVersionAsync(Guid policyId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var newId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var source = await GetPolicyForUpdateAsync(policyId, cancellationToken);
            var rules = await UnitOfWork.RefundPolicyRules.GetByPolicyForUpdateAsync(policyId, cancellationToken);
            var version = await UnitOfWork.RefundPolicies.GetMaxVersionAsync(source.PolicyFamilyId, cancellationToken) + 1;

            // Bản gốc giữ nguyên; bản cũ chỉ bị đóng khi bản mới được kích hoạt.
            await UnitOfWork.RefundPolicies.AddAsync(new RefundPolicy
            {
                RefundPolicyId = newId,
                PolicyFamilyId = source.PolicyFamilyId,
                Version = version,
                CreatedByUserId = ActorId,
                Name = source.Name,
                Description = source.Description,
                PolicyType = source.PolicyType,
                Status = RefundPolicyStatuses.Draft,
                EffectiveFrom = ToUtc(DateOnly.FromDateTime(Now)),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await ReplaceRulesAsync(newId, rules.Select(r => (r.MinHoursBefore, r.RefundPercentage)), cancellationToken);
            await AuditAsync("CloneVersion", EntityType, newId, new { sourcePolicyId = policyId, version }, cancellationToken);
        }, cancellationToken);
        return newId;
    }

    public async Task ActivateAsync(Guid policyId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var policy = await GetPolicyForUpdateAsync(policyId, cancellationToken);
            if (policy.Status == RefundPolicyStatuses.Active)
                throw new ConflictException("Chính sách đang ở trạng thái áp dụng", ErrorCodes.InvalidPolicyState);
            if (policy.Status == RefundPolicyStatuses.Archived
                && !await UnitOfWork.RefundPolicies.IsLatestVersionAsync(policy.PolicyFamilyId, policy.Version, cancellationToken))
                throw new ConflictException("Chỉ phiên bản mới nhất của chính sách mới được áp dụng lại",
                    ErrorCodes.PolicyNotLatestVersion);

            var rules = await UnitOfWork.RefundPolicyRules.GetByPolicyForUpdateAsync(policyId, cancellationToken);
            Check(rules.Count > 0, "rules", "Cần ít nhất một quy tắc hoàn tiền để áp dụng");

            var previousStatus = policy.Status;
            NormalizeEffectiveFrom(policy);
            if (previousStatus == RefundPolicyStatuses.Archived && policy.EffectiveUntil <= Now)
                policy.EffectiveUntil = null; // bản đã hết hạn / bị ngừng: mở lại hiệu lực
            Check(policy.EffectiveUntil == null || policy.EffectiveUntil > policy.EffectiveFrom,
                "effectiveUntil", "Ngày hết hiệu lực phải sau ngày bắt đầu; hãy sửa lại trước khi áp dụng");

            policy.Status = RefundPolicyStatuses.Active;
            policy.UpdatedAt = Now;
            await ApplyActivationEffectsAsync(policy, cancellationToken);
            await AuditAsync("Activate", EntityType, policyId, new { previousStatus, policy.EffectiveFrom }, cancellationToken);
        }, cancellationToken);
    }

    public async Task ArchiveAsync(Guid policyId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var policy = await GetPolicyForUpdateAsync(policyId, cancellationToken);
            if (policy.Status != RefundPolicyStatuses.Active)
                throw new ConflictException("Chỉ chính sách đang áp dụng mới có thể ngừng áp dụng", ErrorCodes.InvalidPolicyState);
            await EnsureNotLastActiveAsync(policy, cancellationToken);

            policy.Status = RefundPolicyStatuses.Archived;
            policy.UpdatedAt = Now;
            await AuditAsync("Archive", EntityType, policyId, new { policy.Version }, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeletePolicyAsync(Guid policyId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var policy = await GetEditableAsync(policyId, cancellationToken);
            await EnsureNotLastActiveAsync(policy, cancellationToken);

            foreach (var rule in await UnitOfWork.RefundPolicyRules.GetByPolicyForUpdateAsync(policyId, cancellationToken))
            {
                rule.IsDeleted = true;
                rule.UpdatedAt = Now;
            }
            policy.IsDeleted = true;
            policy.UpdatedAt = Now;
            await AuditAsync("Delete", EntityType, policyId, new { policy.Name, policy.Version }, cancellationToken);
        }, cancellationToken);
    }

    private async Task<RefundPolicy> GetPolicyForUpdateAsync(Guid policyId, CancellationToken cancellationToken)
        => await UnitOfWork.RefundPolicies.GetForUpdateAsync(policyId, cancellationToken)
           ?? throw new NotFoundException("Không tìm thấy chính sách hoàn tiền", ErrorCodes.NotFound);

    /// <summary>Lấy chính sách để sửa / xoá; 404 nếu không có, 409 POLICY_ALREADY_IN_USE nếu đã có chuyến/đơn dùng.</summary>
    private async Task<RefundPolicy> GetEditableAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var policy = await GetPolicyForUpdateAsync(policyId, cancellationToken);
        if (await UnitOfWork.RefundPolicies.IsInUseAsync(policyId, cancellationToken))
            throw new ConflictException(
                "Chính sách đã có chuyến đi hoặc đơn hàng áp dụng. Hãy tạo phiên bản mới (clone-version) hoặc ngừng áp dụng.",
                ErrorCodes.PolicyAlreadyInUse);
        return policy;
    }

    /// <summary>true khi chính sách đang hiệu lực và không còn chính sách nào khác cùng loại đang hiệu lực.</summary>
    private async Task<bool> IsLastActiveAsync(RefundPolicy policy, DateTime now, CancellationToken cancellationToken)
        => policy.Status == RefundPolicyStatuses.Active
           && RefundPolicyStatuses.ToDisplay(policy.Status, policy.EffectiveFrom, policy.EffectiveUntil, now) == RefundPolicyStatuses.Active
           && await UnitOfWork.RefundPolicies.CountEffectiveActiveAsync(policy.PolicyType, now, policy.RefundPolicyId, cancellationToken) == 0;

    private async Task EnsureNotLastActiveAsync(RefundPolicy policy, CancellationToken cancellationToken)
    {
        if (await IsLastActiveAsync(policy, Now, cancellationToken))
            throw new ConflictException(
                $"Đây là chính sách {policy.PolicyType} duy nhất đang hiệu lực; hãy kích hoạt chính sách khác trước.",
                ErrorCodes.RefundPolicyLastActive);
    }

    /// <summary>
    /// Đóng các bản cũ cùng họ đang hiệu lực (EffectiveUntil = ngày hiệu lực của bản mới); loại Rental đóng thêm
    /// các họ Rental khác để mỗi lúc chỉ có một chính sách Rental hiệu lực.
    /// </summary>
    private async Task ApplyActivationEffectsAsync(RefundPolicy activated, CancellationToken cancellationToken)
    {
        var targets = await UnitOfWork.RefundPolicies.GetOpenForActivationAsync(
            activated.PolicyFamilyId, activated.PolicyType, activated.RefundPolicyId, activated.EffectiveFrom,
            includeOtherFamilies: activated.PolicyType == RefundPolicyTypes.Rental, cancellationToken);
        foreach (var target in targets.Where(t => t.PolicyFamilyId != activated.PolicyFamilyId || t.Version < activated.Version))
        {
            target.EffectiveUntil = activated.EffectiveFrom;
            target.UpdatedAt = Now;
        }
    }

    /// <summary>Ngày bắt đầu nằm trong quá khứ (trước hôm nay) thì áp dụng từ thời điểm hiện tại.</summary>
    private void NormalizeEffectiveFrom(RefundPolicy policy)
    {
        if (policy.EffectiveFrom < ToUtc(DateOnly.FromDateTime(Now)))
            policy.EffectiveFrom = Now;
    }

    /// <summary>Xoá mềm các bậc cũ của chính sách đích rồi thêm bậc mới.</summary>
    private async Task ReplaceRulesAsync(Guid policyId, IEnumerable<(int MinHoursBefore, decimal RefundPercentage)> rules,
        CancellationToken cancellationToken)
    {
        foreach (var old in await UnitOfWork.RefundPolicyRules.GetByPolicyForUpdateAsync(policyId, cancellationToken))
        {
            old.IsDeleted = true;
            old.UpdatedAt = Now;
        }
        await UnitOfWork.RefundPolicyRules.AddRangeAsync(rules.Select(r => new RefundPolicyRule
        {
            RefundPolicyRuleId = Guid.NewGuid(),
            RefundPolicyId = policyId,
            MinHoursBefore = r.MinHoursBefore,
            RefundPercentage = r.RefundPercentage,
            CreatedAt = Now,
            UpdatedAt = Now
        }), cancellationToken);
    }

    private static T FillListItem<T>(T item, AdminRefundPolicyRow x, DateTime now) where T : RefundPolicyListItemResponse
    {
        item.PolicyId = x.RefundPolicyId;
        item.PolicyFamilyId = x.PolicyFamilyId;
        item.Name = x.Name;
        item.Description = x.Description;
        item.PolicyType = x.PolicyType;
        item.Version = x.Version;
        item.VersionsCount = x.VersionsCount;
        item.EffectiveFrom = DateOnly.FromDateTime(x.EffectiveFrom);
        item.EffectiveUntil = x.EffectiveUntil.HasValue ? DateOnly.FromDateTime(x.EffectiveUntil.Value) : null;
        item.Status = x.Status;
        item.DisplayStatus = RefundPolicyStatuses.ToDisplay(x.Status, x.EffectiveFrom, x.EffectiveUntil, now);
        item.RulesCount = x.RulesCount;
        item.TripsCount = x.TripsCount;
        item.OrdersCount = x.OrdersCount;
        item.UsageCount = x.UsageCount;
        item.CreatedByName = x.CreatedByName;
        item.CreatedAt = x.CreatedAt;
        return item;
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static DateTime ToUtc(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
}
