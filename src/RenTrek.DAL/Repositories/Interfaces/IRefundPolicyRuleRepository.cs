using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRefundPolicyRuleRepository : IGenericRepository<RefundPolicyRule>
{
    /// <summary>Các rule chưa xoá của một chính sách (tracked) để thay thế.</summary>
    Task<List<RefundPolicyRule>> GetByPolicyForUpdateAsync(Guid refundPolicyId, CancellationToken cancellationToken = default);
}
