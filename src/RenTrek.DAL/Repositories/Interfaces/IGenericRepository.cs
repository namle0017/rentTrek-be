using System.Linq.Expressions;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IGenericRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>Query chỉ đọc (AsNoTracking) để Service build filter / sort / paging.</summary>
    IQueryable<TEntity> Query();

    /// <summary>Query có tracking, dùng khi cần lấy entity ra để cập nhật.</summary>
    IQueryable<TEntity> QueryTracking();

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);

    void RemoveRange(IEnumerable<TEntity> entities);
}
