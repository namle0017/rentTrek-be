using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    /// <summary>Nhật ký audit cho trang quản trị.</summary>
    Task<(IReadOnlyList<AuditLogRow> Items, int TotalCount)> GetPagedAsync(
        AuditLogFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Audit của đơn và của participant thuộc đơn, mới nhất trước.</summary>
    Task<List<AuditLogRow>> GetForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Số bản ghi audit có hành động <paramref name="action"/> trên thực thể <paramref name="entityId"/>.</summary>
    Task<int> CountAsync(string action, Guid entityId, CancellationToken cancellationToken = default);
}
