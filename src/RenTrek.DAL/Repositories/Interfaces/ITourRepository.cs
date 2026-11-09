using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface ITourRepository : IGenericRepository<Tour>
{
    /// <summary>
    /// Danh sách tour cho khách (GET /tours). Chỉ tour Active/Published, mỗi họ tour đúng 1 thẻ là bản
    /// đang bán có Version lớn nhất. Trips của thẻ gồm cả chuyến Open sắp tới của các bản cũ vẫn đang bán
    /// cùng họ, để publish bản mới không làm mất chuyến đã lên lịch của bản cũ.
    /// </summary>
    Task<(IReadOnlyList<Tour> Items, int TotalCount)> GetPagedToursAsync(
        TourFilterParams filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chi tiết tour cho khách (GET /tours/{id}). Null nếu tour Draft hoặc đã xoá. Bản cũ hoặc
    /// Archived/Expired vẫn mở được để khách xem lại tour đã đặt. Tour đang bán thì Trips có thêm chuyến
    /// Open sắp tới của bản khác cùng họ đang bán; tour Archived/Expired chỉ có chuyến của chính nó.
    /// </summary>
    Task<Tour?> GetTourDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Số tour có id trong danh sách.</summary>
    Task<int> CountByIdsAsync(IReadOnlyCollection<Guid> tourIds, CancellationToken cancellationToken = default);

    /// <summary>Có tour (kể cả đã xoá) dùng địa điểm làm điểm chính.</summary>
    Task<bool> AnyByMainLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>Tour kèm ảnh, hoạt động, đồ tự mang, đồ cấp (có biến thể + sản phẩm); tracked để sửa.</summary>
    Task<Tour?> GetForEditAsync(Guid tourId, CancellationToken cancellationToken = default);

    /// <summary>Đã có đơn đặt chỗ trên bất kỳ chuyến nào của tour (kể cả chuyến/đơn đã xoá).</summary>
    Task<bool> HasBookingsAsync(Guid tourId, CancellationToken cancellationToken = default);

    /// <summary>Mọi phiên bản chưa xoá trong một họ tour, Version giảm dần.</summary>
    Task<IReadOnlyList<TourVersionRow>> GetFamilyVersionsAsync(
        Guid tourFamilyId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Bản mới nhất (Version lớn nhất) của mỗi họ tour chưa xoá, mới cập nhật trước.</summary>
    Task<(IReadOnlyList<AdminTourRow> Items, int TotalCount)> GetAdminPagedAsync(
        AdminTourFilter filter,
        CancellationToken cancellationToken = default);
}
