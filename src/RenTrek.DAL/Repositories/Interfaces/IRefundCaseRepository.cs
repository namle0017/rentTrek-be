using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefundCaseRepository
{
    Task AddAsync(RefundCase refundCase, CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ kèm Order, Payment, người liên quan (chỉ đọc).</summary>
    Task<RefundCase?> GetDetailAsync(Guid refundCaseId, CancellationToken cancellationToken = default);

    /// <summary>Khoá advisory theo hồ sơ rồi lấy bản có tracking. BẮT BUỘC gọi trong transaction đã mở.</summary>
    Task<RefundCase?> GetForUpdateAsync(Guid refundCaseId, CancellationToken cancellationToken = default);

    Task<RefundCase?> GetLatestByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<RefundCase> Items, int TotalItems)> SearchAsync(
        IReadOnlyCollection<string> statuses,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Khoá advisory chung rồi lấy hồ sơ cần xuất (có tracking), bỏ qua hồ sơ của chính người xuất.
    /// Hai Admin xuất cùng lúc không thể nhận trùng hồ sơ. BẮT BUỘC gọi trong transaction đã mở.
    /// </summary>
    Task<IReadOnlyList<RefundCase>> GetForExportAsync(
        string status,
        Guid excludedCustomerUserId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default);

    Task<bool> ProviderTransactionUsedAsync(string providerTransactionId, CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ đã kết thúc trước <paramref name="closedBefore"/> mà vẫn còn payload mã hóa.</summary>
    Task<IReadOnlyList<Guid>> GetPurgeableIdsAsync(DateTimeOffset closedBefore, CancellationToken cancellationToken = default);

    Task<RefundPolicy?> GetPolicyWithRulesAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);

    Task<Guid?> GetLatestCollectionPaymentIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<decimal> GetSucceededRefundTotalAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetActiveUserIdsByRolesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);

    Task AddChallengeAsync(RefundActionChallenge challenge, CancellationToken cancellationToken = default);

    Task<RefundActionChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default);

    /// <summary>Mọi challenge chưa dùng của Admin (có tracking) để vô hiệu khi phát hành mã mới.</summary>
    Task<IReadOnlyList<RefundActionChallenge>> GetOpenChallengesAsync(Guid adminUserId, CancellationToken cancellationToken = default);

    Task<int> CountChallengesSinceAsync(Guid adminUserId, DateTimeOffset since, CancellationToken cancellationToken = default);
}
