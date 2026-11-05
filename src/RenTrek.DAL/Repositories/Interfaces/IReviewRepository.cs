using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IReviewRepository : IGenericRepository<Review>
{
    /// <summary>Đánh giá chưa xoá (participant chưa xoá) cho trang quản trị.</summary>
    Task<(IReadOnlyList<AdminReviewRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminReviewFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Đánh giá chưa xoá theo id (tracking để cập nhật).</summary>
    Task<Review?> GetForUpdateAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
