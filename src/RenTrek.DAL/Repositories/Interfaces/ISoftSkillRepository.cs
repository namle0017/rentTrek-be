using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ISoftSkillRepository : IGenericRepository<SoftSkill>
{
    /// <summary>Kỹ năng chưa xoá, sắp theo tên. Không tracking.</summary>
    Task<List<SoftSkill>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Số kỹ năng chưa xoá có id trong danh sách.</summary>
    Task<int> CountActiveAsync(IReadOnlyCollection<Guid> softSkillIds, CancellationToken cancellationToken = default);

    /// <summary>Kỹ năng chưa xoá, lọc tên (chứa, không phân biệt hoa thường), sắp theo tên. Không tracking.</summary>
    Task<List<SoftSkill>> SearchActiveAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tên đã dùng bởi kỹ năng chưa xoá khác (không phân biệt hoa thường).</summary>
    Task<bool> NameExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default);
}
