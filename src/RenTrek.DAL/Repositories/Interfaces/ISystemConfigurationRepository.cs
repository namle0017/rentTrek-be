using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ISystemConfigurationRepository : IGenericRepository<SystemConfiguration>
{
    /// <summary>Giá trị cấu hình (chưa xoá) theo khoá; null nếu chưa cấu hình.</summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
}
