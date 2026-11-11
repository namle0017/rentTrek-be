using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Chính sách hoàn tiền có phiên bản: bản đã có chuyến/đơn dùng thì bất biến, muốn đổi phải clone phiên bản mới.
/// </summary>
public sealed class AdminRefundPolicyService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminRefundPolicyService
{
    private const string LockKey = "refund-policies";

    public async Task<IReadOnlyList<RefundPolicyListItemResponse>> GetPoliciesAsync(string? policyType, CancellationToken cancellationToken)
    {
        var rows = await UnitOfWork.RefundPolicies.GetAdminListAsync(
            string.IsNullOrWhiteSpace(policyType) ? null : policyType.Trim(), cancellationToken);
        return rows.Select(x => new RefundPolicyListItemResponse
        {
            PolicyId = x.RefundPolicyId,
            PolicyFamilyId = x.PolicyFamilyId,
            Name = x.Name,
            PolicyType = x.PolicyType,
            Version = x.Version,
            EffectiveFrom = DateOnly.FromDateTime(x.EffectiveFrom),
            EffectiveUntil = x.EffectiveUntil.HasValue ? DateOnly.FromDateTime(x.EffectiveUntil.Value) : null,
            Status = x.Status,
            ActiveTripsCount = x.UsageCount
        }).ToList();
    }

    public async Task<RefundPolicyDetailResponse> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var policy = await UnitOfWork.RefundPolicies.GetDetailAsync(policyId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chính sách hoàn tiền", ErrorCodes.NotFound);
        return new RefundPolicyDetailResponse
        {
            PolicyId = policy.RefundPolicyId,
            PolicyFamilyId = policy.PolicyFamilyId,
            Name = policy.Name,
            PolicyType = policy.PolicyType,
            Version = policy.Version,
            EffectiveFrom = DateOnly.FromDateTime(policy.EffectiveFrom),
            EffectiveUntil = policy.EffectiveUntil.HasValue ? DateOnly.FromDateTime(policy.EffectiveUntil.Value) : null,
            Status = policy.Status,
            IsLocked = await UnitOfWork.RefundPolicies.IsInUseAsync(policyId, cancellationToken),
            Rules = policy.RefundPolicyRules.Select(r => new RefundPolicyRuleResponse
            {
                RuleId = r.RefundPolicyRuleId,
                MinHoursBefore = r.MinHoursBefore,
                RefundPercentage = r.RefundPercentage,
                Description = $"Hủy trước ≥ {r.MinHoursBefore} giờ: hoàn {r.RefundPercentage:0.##}%"
            }).ToList()
        };
    }

    public async Task<Guid> CreatePolicyAsync(SaveRefundPolicyRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var policyId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            await UnitOfWork.RefundPolicies.AddAsync(new RefundPolicy
            {
                RefundPolicyId = policyId,
                PolicyFamilyId = Guid.NewGuid(),
                Version = 1,
                CreatedByUserId = ActorId,
                Name = request.Name.Trim(),
                PolicyType = request.PolicyType,
                Status = RefundPolicyStatuses.Active,
                EffectiveFrom = ToUtc(request.EffectiveFrom),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await AuditAsync("Create", "RefundPolicy", policyId, request, cancellationToken);
        }, cancellationToken);
        return policyId;
    }

    public async Task UpdatePolicyAsync(Guid policyId, SaveRefundPolicyRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync(LockKey, async () =>
        {
            var policy = await GetEditableAsync(policyId, cancellationToken);
            var before = new { policy.Name, policy.PolicyType, policy.EffectiveFrom };
            policy.Name = request.Name.Trim();
            policy.PolicyType = request.PolicyType;
            policy.EffectiveFrom = ToUtc(request.EffectiveFrom);
            policy.UpdatedAt = Now;
            await AuditAsync("Update", "RefundPolicy", policyId, new { before, after = request }, cancellationToken);
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
            await AuditAsync("Update", "RefundPolicy", policyId, new { rules = request.Rules }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Guid> CloneVersionAsync(Guid policyId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var newId = Guid.NewGuid();
        await AtomicAsync(LockKey, async () =>
        {
            var source = await UnitOfWork.RefundPolicies.GetForUpdateAsync(policyId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy chính sách hoàn tiền", ErrorCodes.NotFound);
            var rules = await UnitOfWork.RefundPolicyRules.GetByPolicyForUpdateAsync(policyId, cancellationToken);
            var version = await UnitOfWork.RefundPolicies.GetMaxVersionAsync(source.PolicyFamilyId, cancellationToken) + 1;

            if (source.EffectiveUntil == null || source.EffectiveUntil > Now) source.EffectiveUntil = Now;
            source.UpdatedAt = Now;
            await UnitOfWork.RefundPolicies.AddAsync(new RefundPolicy
            {
                RefundPolicyId = newId,
                PolicyFamilyId = source.PolicyFamilyId,
                Version = version,
                CreatedByUserId = ActorId,
                Name = source.Name,
                Description = source.Description,
                PolicyType = source.PolicyType,
                Status = RefundPolicyStatuses.Active,
                EffectiveFrom = Now,
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await ReplaceRulesAsync(newId, rules.Select(r => (r.MinHoursBefore, r.RefundPercentage)), cancellationToken);
            await AuditAsync("CloneVersion", "RefundPolicy", newId, new { sourcePolicyId = policyId, version }, cancellationToken);
        }, cancellationToken);
        return newId;
    }

    /// <summary>Lấy chính sách để sửa; 404 nếu không có, 400 POLICY_ALREADY_IN_USE nếu đã có chuyến/đơn dùng.</summary>
    private async Task<RefundPolicy> GetEditableAsync(Guid policyId, CancellationToken cancellationToken)
    {
        var policy = await UnitOfWork.RefundPolicies.GetForUpdateAsync(policyId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chính sách hoàn tiền", ErrorCodes.NotFound);
        if (await UnitOfWork.RefundPolicies.IsInUseAsync(policyId, cancellationToken))
            throw new BadRequestException(
                "Chính sách đã có chuyến đi hoặc đơn hàng áp dụng. Hãy nhân bản phiên bản mới (clone-version).",
                ErrorCodes.PolicyAlreadyInUse);
        return policy;
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

    private static DateTime ToUtc(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
}
