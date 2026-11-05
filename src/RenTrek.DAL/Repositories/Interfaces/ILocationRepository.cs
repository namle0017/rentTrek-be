using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ILocationRepository : IGenericRepository<Location>
{
    /// <summary>Địa điểm chưa xoá, lọc tên (chứa, không phân biệt hoa thường), sắp theo tên.</summary>
    Task<List<AdminLocationRow>> SearchActiveAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tên đã dùng bởi địa điểm chưa xoá khác (không phân biệt hoa thường).</summary>
    Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);
}
