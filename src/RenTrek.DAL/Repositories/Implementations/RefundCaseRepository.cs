using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Extensions;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Truy cập dữ liệu hồ sơ hoàn tiền và OTP đi kèm qua EF Core.
/// </summary>
public sealed class RefundCaseRepository(AppDbContext context) : IRefundCaseRepository
{
    public Task AcquireChallengeLockAsync(Guid adminUserId, CancellationToken cancellationToken = default)
        => PostgresAdvisoryLock.AcquireAsync(context, $"refund-otp:{adminUserId:N}", cancellationToken);

    public Task AddAsync(RefundCase refundCase, CancellationToken cancellationToken = default)
        => context.RefundCases.AddAsync(refundCase, cancellationToken).AsTask();

    public Task<RefundCase?> GetDetailAsync(Guid refundCaseId, CancellationToken cancellationToken = default)
        => Graph().AsNoTracking().SingleOrDefaultAsync(x => x.RefundCaseId == refundCaseId, cancellationToken);

    public async Task<RefundCase?> GetForUpdateAsync(Guid refundCaseId, CancellationToken cancellationToken = default)
    {
        await PostgresAdvisoryLock.AcquireAsync(context, $"refund-case:{refundCaseId:N}", cancellationToken);
        return await Graph().SingleOrDefaultAsync(x => x.RefundCaseId == refundCaseId, cancellationToken);
    }

    public Task ReloadOrderAsync(Order order, CancellationToken cancellationToken = default)
        => context.Entry(order).ReloadAsync(cancellationToken);

    public Task<RefundCase?> GetLatestByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Graph().AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<RefundCase> Items, int TotalItems)> SearchByCustomerAsync(
        Guid customerUserId,
        IReadOnlyCollection<string> statuses,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Graph().AsNoTracking().Where(x => x.CustomerUserId == customerUserId);
        if (statuses.Count > 0)
            query = query.Where(x => statuses.Contains(x.Status));

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalItems);
    }

    private static readonly string[] ClosedStatuses = ["Completed", "Rejected", "Failed", "Cancelled"];

    public Task<bool> HasOtherOpenCaseAsync(Guid orderId, Guid excludedRefundCaseId, CancellationToken cancellationToken = default)
        => context.RefundCases.AnyAsync(
            x => x.OrderId == orderId && x.RefundCaseId != excludedRefundCaseId && !x.IsDeleted && !ClosedStatuses.Contains(x.Status),
            cancellationToken);

    public async Task<(IReadOnlyList<RefundCase> Items, int TotalItems)> SearchAsync(
        IReadOnlyCollection<string> statuses,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Graph().AsNoTracking().Where(x => statuses.Contains(x.Status));
        if (fromDate is not null)
            query = query.Where(x => x.CreatedAt >= fromDate);
        if (toDate is not null)
            query = query.Where(x => x.CreatedAt <= toDate);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalItems);
    }

    public async Task<IReadOnlyList<RefundCase>> GetForExportAsync(
        IReadOnlyCollection<string> statuses,
        Guid excludedCustomerUserId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default)
    {
        await PostgresAdvisoryLock.AcquireAsync(context, "refund-export", cancellationToken);

        var query = Graph().Where(x => statuses.Contains(x.Status) && x.CustomerUserId != excludedCustomerUserId);
        if (fromDate is not null)
            query = query.Where(x => x.CreatedAt >= fromDate);
        if (toDate is not null)
            query = query.Where(x => x.CreatedAt <= toDate);
        return await query.OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    // Mã hồ sơ hiển thị "HT-" + 8 ký tự đầu của GUID (không dấu gạch); chỉ tìm trong hồ sơ đã xuất cho kế toán
    public async Task<IReadOnlyList<Guid>> FindApprovedIdsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var prefix = code.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(prefix, "^ht-[0-9a-f]{8}$"))
            return [];

        var hex = prefix[3..];
        return await context.RefundCases
            .FromSqlInterpolated($"SELECT * FROM refund_cases WHERE status = 'Approved' AND NOT is_deleted AND left(replace(refund_case_id::text, '-', ''), 8) = {hex}")
            .AsNoTracking()
            .Select(x => x.RefundCaseId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ProviderTransactionUsedAsync(string providerTransactionId, CancellationToken cancellationToken = default)
        => context.RefundCases.AnyAsync(x => x.ProviderTransactionId == providerTransactionId, cancellationToken);

    // Hồ sơ không hoàn tất (từ chối / hủy / thất bại) cũng không còn lý do giữ số tài khoản
    public async Task<IReadOnlyList<Guid>> GetPurgeableIdsAsync(DateTimeOffset closedBefore, CancellationToken cancellationToken = default)
        => await context.RefundCases
            .Where(x => x.PayoutDetailsCiphertext != null &&
                        ((x.Status == "Completed" && x.CompletedAt <= closedBefore) ||
                         ((x.Status == "Rejected" || x.Status == "Cancelled" || x.Status == "Failed") && x.UpdatedAt <= closedBefore)))
            .Select(x => x.RefundCaseId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefundCase>> GetAwaitingBankInfoAsync(DateTimeOffset requestedBefore, CancellationToken cancellationToken = default)
        => await context.RefundCases.AsNoTracking()
            .Include(x => x.Order)
            .Where(x => !x.IsDeleted && x.Status == "AwaitingBankInfo" && x.BankInfoRequestedAt != null && x.BankInfoRequestedAt <= requestedBefore)
            .OrderBy(x => x.BankInfoRequestedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

    public Task<RefundPolicy?> GetPolicyWithRulesAsync(Guid refundPolicyId, CancellationToken cancellationToken = default)
        => context.RefundPolicies.AsNoTracking()
            .Include(x => x.RefundPolicyRules.Where(rule => !rule.IsDeleted))
            .SingleOrDefaultAsync(x => x.RefundPolicyId == refundPolicyId, cancellationToken);

    public Task<Guid?> GetLatestCollectionPaymentIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => context.Payments
            .Where(x => x.OrderId == orderId && x.Status == "Succeeded" && x.PaymentType != "Refund")
            .OrderByDescending(x => x.ProcessedAt)
            .Select(x => (Guid?)x.PaymentId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<decimal> GetSucceededRefundTotalAsync(Guid orderId, CancellationToken cancellationToken = default)
        => await context.Payments
            .Where(x => x.OrderId == orderId && x.Status == "Succeeded" && x.PaymentType == "Refund")
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    public async Task<IReadOnlyList<Guid>> GetActiveUserIdsByRolesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
        => await context.Accounts
            .Where(x => roleNames.Contains(x.Role.RoleName) && x.Status == "Active" && !x.IsDeleted)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

    public Task AddChallengeAsync(RefundActionChallenge challenge, CancellationToken cancellationToken = default)
        => context.RefundActionChallenges.AddAsync(challenge, cancellationToken).AsTask();

    public Task<RefundActionChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
        => context.RefundActionChallenges.SingleOrDefaultAsync(x => x.ChallengeId == challengeId, cancellationToken);

    public async Task<IReadOnlyList<RefundActionChallenge>> GetOpenChallengesAsync(
        Guid adminUserId,
        CancellationToken cancellationToken = default)
        => await context.RefundActionChallenges
            .Where(x => x.AdminUserId == adminUserId && x.UsedAt == null)
            .ToListAsync(cancellationToken);

    public Task<int> CountChallengesSinceAsync(
        Guid adminUserId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
        => context.RefundActionChallenges.CountAsync(
            x => x.AdminUserId == adminUserId && x.CreatedAt >= since,
            cancellationToken);

    private IQueryable<RefundCase> Graph()
        => context.RefundCases
            .AsSplitQuery()
            .Where(x => !x.IsDeleted)
            .Include(x => x.RefundPayment)
            .Include(x => x.CustomerUser)
            .Include(x => x.RefundPolicyRule)
            .Include(x => x.Proofs.Where(proof => !proof.IsDeleted))
            .Include(x => x.Order).ThenInclude(x => x.RefundPolicy)
            .Include(x => x.Order).ThenInclude(x => x.TripBookingOrder).ThenInclude(x => x.Trip).ThenInclude(x => x.Tour);
}
