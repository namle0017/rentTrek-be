using RenTrek.BLL.Constants;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Helpers;

/// <summary>Bản sao bất biến của policy thuê đồ để tính toán không cần DB.</summary>
public sealed record RentalPolicySnapshot(
    Guid RentalPolicyId,
    string Name,
    int Version,
    decimal DepositPercent,
    int FreeCancelLimit,
    int FreeCancelWindowDays,
    decimal OverLimitCancelFeePercent,
    int ReturnGraceHours,
    int ExtensionMaxDaysLate,
    int SuspectedLossAfterDays,
    int ReceiptAutoConfirmHours,
    int MinPhotosPerAsset,
    IReadOnlyList<RentalPenaltyTier> PickupTiers,
    IReadOnlyList<RentalPenaltyTier> ReturnTiers)
{
    public static RentalPolicySnapshot From(RentalPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        RentalPenaltyTier[] Tiers(string scope) => policy.Tiers
            .Where(t => t.Scope == scope)
            .OrderBy(t => t.MinDaysLate)
            .Select(t => new RentalPenaltyTier(t.MinDaysLate, t.PercentOfDeposit, t.IsTerminal))
            .ToArray();

        return new RentalPolicySnapshot(
            policy.RentalPolicyId,
            policy.Name,
            policy.Version,
            policy.DepositPercent,
            policy.FreeCancelLimit,
            policy.FreeCancelWindowDays,
            policy.OverLimitCancelFeePercent,
            policy.ReturnGraceHours,
            policy.ExtensionMaxDaysLate,
            policy.SuspectedLossAfterDays,
            policy.ReceiptAutoConfirmHours,
            policy.MinPhotosPerAsset,
            Tiers(RentalPolicyTierScopes.PickupLate),
            Tiers(RentalPolicyTierScopes.ReturnLate));
    }
}
