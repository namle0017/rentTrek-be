using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITourRepository : IGenericRepository<Tour>
{
    Task<(IReadOnlyList<Tour> Items, int TotalCount)> GetPagedToursAsync(
        TourFilterParams filter,
        CancellationToken cancellationToken = default);

    Task<Tour?> GetTourDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Số tour có id trong danh sách.</summary>
    Task<int> CountByIdsAsync(IReadOnlyCollection<Guid> tourIds, CancellationToken cancellationToken = default);

    /// <summary>Có tour (kể cả đã xoá) dùng địa điểm làm điểm chính.</summary>
    Task<bool> AnyByMainLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>Bản mới nhất (Version lớn nhất) của mỗi họ tour chưa xoá, mới cập nhật trước.</summary>
    Task<(IReadOnlyList<AdminTourRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminTourFilter filter,
        CancellationToken cancellationToken = default);
}
