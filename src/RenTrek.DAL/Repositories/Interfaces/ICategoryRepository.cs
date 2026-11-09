using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    /// <summary>Danh mục chưa xoá, lọc tên (chứa) và loại (không phân biệt hoa thường), sắp theo tên. Không tracking.</summary>
    Task<List<Category>> SearchActiveAsync(string? search, string? categoryType, CancellationToken cancellationToken = default);

    /// <summary>Tên đã dùng bởi danh mục chưa xoá khác (không phân biệt hoa thường).</summary>
    Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);
}
