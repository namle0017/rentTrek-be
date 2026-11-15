using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefundCaseRepository
{
    Task AcquireChallengeLockAsync(Guid adminUserId, CancellationToken cancellationToken = default);
    Task AddAsync(RefundCase refundCase, CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ kèm Order, Payment, người liên quan (chỉ đọc).</summary>
    Task<RefundCase?> GetDetailAsync(Guid refundCaseId, CancellationToken cancellationToken = default);

    /// <summary>Khoá advisory theo hồ sơ rồi lấy bản có tracking. BẮT BUỘC gọi trong transaction đã mở.</summary>
    Task<RefundCase?> GetForUpdateAsync(Guid refundCaseId, CancellationToken cancellationToken = default);

    /// <summary>Tải lại Order sau khi đã lấy advisory lock chung với webhook.</summary>
    Task ReloadOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task<RefundCase?> GetLatestByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ hoàn tiền của một khách (mới nhất trước); statuses rỗng = mọi trạng thái.</summary>
    Task<(IReadOnlyList<RefundCase> Items, int TotalItems)> SearchByCustomerAsync(
        Guid customerUserId,
        IReadOnlyCollection<string> statuses,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Đơn còn hồ sơ khác chưa kết thúc (unique index ux_refund_cases_open_order).</summary>
    Task<bool> HasOtherOpenCaseAsync(Guid orderId, Guid excludedRefundCaseId, CancellationToken cancellationToken = default);

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
        IReadOnlyCollection<string> statuses,
        Guid excludedCustomerUserId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default);

    /// <summary>ID các hồ sơ Approved có mã hiển thị (HT-xxxxxxxx) khớp; dùng khi file Excel không có cột RefundCaseId.</summary>
    Task<IReadOnlyList<Guid>> FindApprovedIdsByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> ProviderTransactionUsedAsync(string providerTransactionId, CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ đã kết thúc trước <paramref name="closedBefore"/> mà vẫn còn payload mã hóa.</summary>
    Task<IReadOnlyList<Guid>> GetPurgeableIdsAsync(DateTimeOffset closedBefore, CancellationToken cancellationToken = default);

    /// <summary>Hồ sơ đang chờ khách nhập STK, yêu cầu từ trước mốc (để nhắc), kèm đơn (chỉ đọc).</summary>
    Task<IReadOnlyList<RefundCase>> GetAwaitingBankInfoAsync(DateTimeOffset requestedBefore, CancellationToken cancellationToken = default);

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
