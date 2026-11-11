using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITagRepository : IGenericRepository<Tag>
{
    /// <summary>Tag chưa xoá, sắp theo tên. Không tracking.</summary>
    Task<List<Tag>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Số tag chưa xoá có id trong danh sách.</summary>
    Task<int> CountActiveAsync(IReadOnlyCollection<Guid> tagIds, CancellationToken cancellationToken = default);

    /// <summary>Tag chưa xoá, lọc tên (chứa, không phân biệt hoa thường), sắp theo tên, kèm số bài chưa xoá.</summary>
    Task<List<AdminTagRow>> SearchActiveAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tên đã dùng bởi tag chưa xoá khác (không phân biệt hoa thường).</summary>
    Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);

    /// <summary>Slug đã dùng bởi tag khác, kể cả tag đã xoá (DB unique).</summary>
    Task<bool> SlugExistsAsync(string slug, Guid excludeId, CancellationToken cancellationToken = default);
}
