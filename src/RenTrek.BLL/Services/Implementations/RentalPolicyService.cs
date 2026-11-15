using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.RentalPolicy.Requests;
using RenTrek.BLL.DTOs.RentalPolicy.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Chính sách thuê đồ có version. Đơn snapshot <c>rental_policy_id</c> lúc đặt nên đổi policy không ảnh hưởng đơn cũ.
/// </summary>
public sealed class RentalPolicyService(IUnitOfWork unitOfWork, TimeProvider timeProvider) : IRentalPolicyService
{
    /// <summary>Mặc định khi DB chưa có policy (khớp seed trong migration 20261114).</summary>
    public static readonly RentalPolicySnapshot Default = new(
        Guid.Empty,
        "Chính sách thuê đồ mặc định",
        1,
        DepositPercent: 20,
        FreeCancelLimit: 2,
        FreeCancelWindowDays: 30,
        OverLimitCancelFeePercent: 5,
        ReturnGraceHours: 2,
        ExtensionMaxDaysLate: 2,
        SuspectedLossAfterDays: 7,
        ReceiptAutoConfirmHours: 24,
        MinPhotosPerAsset: 1,
        PickupTiers: [new(1, 5, false), new(2, 20, false), new(3, 100, true)],
        ReturnTiers: [new(3, 5, false), new(4, 20, false), new(5, 100, true)]);

    public async Task<RentalPolicySnapshot> GetActiveSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var policy = await unitOfWork.RentalPolicies.GetActiveWithTiersAsync(cancellationToken);
        return policy is null ? Default : RentalPolicySnapshot.From(policy);
    }

    public async Task<RentalPolicySnapshot> GetSnapshotAsync(Guid? rentalPolicyId, CancellationToken cancellationToken = default)
    {
        if (rentalPolicyId is { } id && id != Guid.Empty)
        {
            var policy = await unitOfWork.RentalPolicies.GetWithTiersAsync(id, cancellationToken);
            if (policy is not null)
                return RentalPolicySnapshot.From(policy);
        }

        return await GetActiveSnapshotAsync(cancellationToken);
    }

    public async Task<RentalPolicyResponse> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var policy = await unitOfWork.RentalPolicies.GetActiveWithTiersAsync(cancellationToken);
        return policy is null ? ToResponse(Default) : Map(policy);
    }

    public async Task<IReadOnlyList<AdminRentalPolicyResponse>> GetAllAsync(CancellationToken cancellationToken = default)
        => await ToAdminResponsesAsync(await unitOfWork.RentalPolicies.GetAllWithTiersAsync(cancellationToken), cancellationToken);

    public async Task<AdminRentalPolicyResponse> GetByIdAsync(Guid rentalPolicyId, CancellationToken cancellationToken = default)
    {
        var policy = await unitOfWork.RentalPolicies.GetWithTiersAsync(rentalPolicyId, cancellationToken);
        if (policy is null || policy.IsDeleted)
            throw new NotFoundException("Không tìm thấy chính sách thuê đồ", ErrorCodes.NotFound);
        return (await ToAdminResponsesAsync([policy], cancellationToken)).Single();
    }

    private async Task<IReadOnlyList<AdminRentalPolicyResponse>> ToAdminResponsesAsync(
        IReadOnlyCollection<RentalPolicy> policies, CancellationToken cancellationToken)
    {
        if (policies.Count == 0) return [];
        var counts = await unitOfWork.RentalPolicies.GetOrderCountsAsync(
            policies.Select(p => p.RentalPolicyId).ToArray(), cancellationToken);
        var names = await unitOfWork.RentalPolicies.GetCreatorNamesAsync(
            policies.Where(p => p.CreatedByUserId.HasValue).Select(p => p.CreatedByUserId!.Value).Distinct().ToArray(),
            cancellationToken);
        return policies.Select(p =>
        {
            var response = Fill(new AdminRentalPolicyResponse(), p);
            response.OrdersCount = counts.GetValueOrDefault(p.RentalPolicyId);
            response.CreatedByName = p.CreatedByUserId is { } id ? names.GetValueOrDefault(id) : null;
            response.CreatedAt = RentalPeriodCalculator.ToUtc(p.CreatedAt);
            return response;
        }).ToList();
    }

    public async Task<RentalPolicyResponse> CreateVersionAsync(
        Guid adminUserId,
        CreateRentalPolicyVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var current = await unitOfWork.RentalPolicies.GetActiveForUpdateAsync(cancellationToken);
            var familyId = current?.PolicyFamilyId ?? Guid.NewGuid();
            var version = await unitOfWork.RentalPolicies.GetMaxVersionAsync(familyId, cancellationToken) + 1;

            if (current is not null)
            {
                current.Status = RentalPolicyStatuses.Archived;
                current.EffectiveUntil = now;
                current.UpdatedAt = now;
                // Archive trước để không vi phạm unique "một policy Active"
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var policyId = Guid.NewGuid();
            var policy = new RentalPolicy
            {
                RentalPolicyId = policyId,
                PolicyFamilyId = familyId,
                Version = version,
                Name = request.Name.Trim(),
                Status = RentalPolicyStatuses.Active,
                DepositPercent = request.DepositPercent,
                FreeCancelLimit = request.FreeCancelLimit,
                FreeCancelWindowDays = request.FreeCancelWindowDays,
                OverLimitCancelFeePercent = request.OverLimitCancelFeePercent,
                ReturnGraceHours = request.ReturnGraceHours,
                ExtensionMaxDaysLate = request.ExtensionMaxDaysLate,
                SuspectedLossAfterDays = request.SuspectedLossAfterDays,
                ReceiptAutoConfirmHours = request.ReceiptAutoConfirmHours,
                MinPhotosPerAsset = request.MinPhotosPerAsset,
                EffectiveFrom = now,
                CreatedByUserId = adminUserId,
                CreatedAt = now,
                UpdatedAt = now,
                Tiers = request.Tiers.Select(t => new RentalPolicyTier
                {
                    RentalPolicyTierId = Guid.NewGuid(),
                    RentalPolicyId = policyId,
                    Scope = t.Scope,
                    MinDaysLate = t.MinDaysLate,
                    PercentOfDeposit = t.IsTerminal ? 100 : t.PercentOfDeposit,
                    IsTerminal = t.IsTerminal,
                    CreatedAt = now
                }).ToList()
            };
            await unitOfWork.RentalPolicies.AddAsync(policy, cancellationToken);
            await unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                ActorUserId = adminUserId,
                Action = "RentalPolicyCreateVersion",
                EntityType = "RentalPolicy",
                EntityId = policyId,
                Details = JsonSerializer.Serialize(new { previousPolicyId = current?.RentalPolicyId, version, request }),
                CreatedAt = now
            }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return Map(policy);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public RentalPolicyResponse ToResponse(RentalPolicySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return FillFromSnapshot(new RentalPolicyResponse(), snapshot);
    }

    private static T FillFromSnapshot<T>(T response, RentalPolicySnapshot snapshot) where T : RentalPolicyResponse
    {
        response.RentalPolicyId = snapshot.RentalPolicyId == Guid.Empty ? null : snapshot.RentalPolicyId;
        response.Version = snapshot.Version;
        response.Name = snapshot.Name;
        response.Status = RentalPolicyStatuses.Active;
        response.DepositPercent = snapshot.DepositPercent;
        response.FreeCancelLimit = snapshot.FreeCancelLimit;
        response.FreeCancelWindowDays = snapshot.FreeCancelWindowDays;
        response.OverLimitCancelFeePercent = snapshot.OverLimitCancelFeePercent;
        response.ReturnGraceHours = snapshot.ReturnGraceHours;
        response.ExtensionMaxDaysLate = snapshot.ExtensionMaxDaysLate;
        response.SuspectedLossAfterDays = snapshot.SuspectedLossAfterDays;
        response.ReceiptAutoConfirmHours = snapshot.ReceiptAutoConfirmHours;
        response.MinPhotosPerAsset = snapshot.MinPhotosPerAsset;
        response.Tiers = snapshot.PickupTiers.Select(t => MapTier(RentalPolicyTierScopes.PickupLate, t))
            .Concat(snapshot.ReturnTiers.Select(t => MapTier(RentalPolicyTierScopes.ReturnLate, t)))
            .ToList();
        return response;
    }

    private static RentalPolicyTierResponse MapTier(string scope, RentalPenaltyTier tier) => new()
    {
        Scope = scope,
        MinDaysLate = tier.MinDaysLate,
        PercentOfDeposit = tier.PercentOfDeposit,
        IsTerminal = tier.IsTerminal
    };

    private RentalPolicyResponse Map(RentalPolicy policy) => Fill(new RentalPolicyResponse(), policy);

    private static T Fill<T>(T response, RentalPolicy policy) where T : RentalPolicyResponse
    {
        FillFromSnapshot(response, RentalPolicySnapshot.From(policy));
        response.PolicyFamilyId = policy.PolicyFamilyId;
        response.Status = policy.Status;
        response.EffectiveFrom = RentalPeriodCalculator.ToUtc(policy.EffectiveFrom);
        response.EffectiveUntil = policy.EffectiveUntil is { } until ? RentalPeriodCalculator.ToUtc(until) : null;
        return response;
    }

    private static void Validate(CreateRentalPolicyVersionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 255)
            errors["name"] = ["Tên chính sách bắt buộc, tối đa 255 ký tự"];
        if (request.DepositPercent is < 0 or > 100)
            errors["depositPercent"] = ["% cọc phải từ 0 đến 100"];
        if (request.OverLimitCancelFeePercent is < 0 or > 100)
            errors["overLimitCancelFeePercent"] = ["% phí huỷ phải từ 0 đến 100"];
        if (request.FreeCancelLimit < 0 || request.FreeCancelWindowDays <= 0)
            errors["freeCancelLimit"] = ["Giới hạn huỷ miễn phí không hợp lệ"];
        if (request.ReturnGraceHours < 0 || request.ExtensionMaxDaysLate < 0 || request.SuspectedLossAfterDays <= 0
            || request.ReceiptAutoConfirmHours <= 0 || request.MinPhotosPerAsset < 0)
            errors["timing"] = ["Các mốc thời gian không hợp lệ"];

        foreach (var scope in new[] { RentalPolicyTierScopes.PickupLate, RentalPolicyTierScopes.ReturnLate })
        {
            var tiers = request.Tiers.Where(t => t.Scope == scope).OrderBy(t => t.MinDaysLate).ToList();
            if (tiers.Count == 0)
            {
                errors[$"tiers.{scope}"] = ["Cần ít nhất một bậc phạt"];
                continue;
            }

            if (tiers.Select(t => t.MinDaysLate).Distinct().Count() != tiers.Count || tiers.Any(t => t.MinDaysLate <= 0))
                errors[$"tiers.{scope}"] = ["Số ngày trễ của các bậc phải lớn hơn 0 và không trùng nhau"];
            else if (tiers.Any(t => t.PercentOfDeposit is < 0 or > 100))
                errors[$"tiers.{scope}"] = ["% cọc của bậc phải từ 0 đến 100"];
            else if (tiers.Zip(tiers.Skip(1)).Any(p => p.Second.PercentOfDeposit < p.First.PercentOfDeposit))
                errors[$"tiers.{scope}"] = ["Bậc sau không được thấp hơn bậc trước"];
            else if (tiers.Count(t => t.IsTerminal) != 1 || !tiers[^1].IsTerminal)
                errors[$"tiers.{scope}"] = ["Phải có đúng một bậc mất cọc và là bậc cuối"];
        }

        if (request.Tiers.Any(t => t.Scope is not (RentalPolicyTierScopes.PickupLate or RentalPolicyTierScopes.ReturnLate)))
            errors["tiers"] = ["Scope chỉ nhận PickupLate hoặc ReturnLate"];

        if (errors.Count > 0)
            throw new ValidationException(errors, "Chính sách thuê đồ không hợp lệ", ErrorCodes.ValidationError);
    }
}
