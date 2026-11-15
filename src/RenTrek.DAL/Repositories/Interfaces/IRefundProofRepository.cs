using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefundProofRepository
{
    Task AddAsync(RefundProof proof, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<RefundProof> proofs, CancellationToken cancellationToken = default);

    /// <summary>Chứng từ chưa xóa theo ID (chỉ đọc).</summary>
    Task<RefundProof?> GetByIdAsync(Guid refundProofId, CancellationToken cancellationToken = default);

    /// <summary>Chứng từ của các khoản hoàn (payment), sắp theo thời gian tạo (chỉ đọc).</summary>
    Task<IReadOnlyList<RefundProof>> GetByPaymentIdsAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken cancellationToken = default);
}
