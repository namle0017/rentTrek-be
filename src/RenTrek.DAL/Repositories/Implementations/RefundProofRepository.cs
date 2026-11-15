using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.DAL.Repositories.Implementations;

/// <summary>
/// Truy cập dữ liệu chứng từ hoàn tiền qua EF Core.
/// </summary>
public sealed class RefundProofRepository(AppDbContext context) : IRefundProofRepository
{
    public Task AddAsync(RefundProof proof, CancellationToken cancellationToken = default)
        => context.RefundProofs.AddAsync(proof, cancellationToken).AsTask();

    public Task AddRangeAsync(IEnumerable<RefundProof> proofs, CancellationToken cancellationToken = default)
        => context.RefundProofs.AddRangeAsync(proofs, cancellationToken);

    public Task<RefundProof?> GetByIdAsync(Guid refundProofId, CancellationToken cancellationToken = default)
        => context.RefundProofs.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RefundProofId == refundProofId && !x.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<RefundProof>> GetByPaymentIdsAsync(
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default)
        => paymentIds.Count == 0
            ? []
            : await context.RefundProofs.AsNoTracking()
                .Where(x => x.PaymentId != null && paymentIds.Contains(x.PaymentId.Value) && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
}
