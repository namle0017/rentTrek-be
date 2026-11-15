using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.BLL.Helpers;

internal static class TripRefundPolicyResolver
{
    public static async Task<RefundPolicy?> ResolveAsync(
        IRefundPolicyRepository policies,
        Guid tripPolicyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var specific = tripPolicyId == Guid.Empty
            ? null
            : await policies.GetDetailAsync(tripPolicyId, cancellationToken);
        if (specific is { PolicyType: RefundPolicyTypes.Tour, IsDeleted: false } &&
            specific.Status != RefundPolicyStatuses.Draft)
            return specific;

        return await policies.GetCurrentAsync(RefundPolicyTypes.Tour, RefundPolicyStatuses.Active, now.UtcDateTime, cancellationToken);
    }

    public static async Task EnsureCanOpenAsync(
        IRefundPolicyRepository policies,
        Trip trip,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await ResolveAsync(policies, trip.RefundPolicyId, now, cancellationToken) is null)
            throw new ConflictException("Chuyến đi chưa có chính sách hoàn tiền", ErrorCodes.RefundPolicyChanged);
    }
}
