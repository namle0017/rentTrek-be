using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    // Query đặc thù của AuditLog khai báo tại đây
}
