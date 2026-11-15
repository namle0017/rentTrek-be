using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRentalPolicyRepository : IGenericRepository<RentalPolicy>
{
    /// <summary>Policy đang Active kèm các bậc phạt (chỉ đọc); null nếu chưa cấu hình.</summary>
    Task<RentalPolicy?> GetActiveWithTiersAsync(CancellationToken cancellationToken = default);

    /// <summary>Policy theo id kèm các bậc phạt (chỉ đọc), kể cả bản đã Archived (đơn cũ snapshot).</summary>
    Task<RentalPolicy?> GetWithTiersAsync(Guid rentalPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Policy có tracking kèm bậc để admin kích hoạt / archive.</summary>
    Task<RentalPolicy?> GetForUpdateAsync(Guid rentalPolicyId, CancellationToken cancellationToken = default);

    /// <summary>Policy đang Active có tracking (để archive khi kích hoạt bản mới).</summary>
    Task<RentalPolicy?> GetActiveForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>Mọi policy chưa xoá kèm bậc phạt (chỉ đọc), version giảm dần rồi mới tạo trước.</summary>
    Task<IReadOnlyList<RentalPolicy>> GetAllWithTiersAsync(CancellationToken cancellationToken = default);

    /// <summary>Số đơn thuê snapshot từng policy (chỉ có khoá khi số đơn &gt; 0).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetOrderCountsAsync(
        IReadOnlyCollection<Guid> rentalPolicyIds, CancellationToken cancellationToken = default);

    /// <summary>Họ tên người tạo theo user id (kể cả tài khoản đã xoá).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetCreatorNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    Task<int> GetMaxVersionAsync(Guid policyFamilyId, CancellationToken cancellationToken = default);
}
