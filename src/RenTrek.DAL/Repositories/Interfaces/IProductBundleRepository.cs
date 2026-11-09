using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IProductBundleRepository : IGenericRepository<ProductBundle>
{
    /// <summary>
    /// Thông tin combo (kèm thành phần chưa xoá, theo thứ tự tạo) để dựng dòng thuê. Trả cả combo đã xoá / ngừng bán
    /// với <c>IsActive = false</c>; id không tồn tại thì không có trong kết quả.
    /// </summary>
    Task<List<RentalBundleInfo>> GetActiveForRentalAsync(
        IReadOnlyCollection<Guid> bundleIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Danh sách combo hiển thị cho khách (combo Active chưa xoá, danh mục Active, có bậc giá và có thành phần còn hiệu lực),
    /// lọc / sắp xếp / phân trang theo <paramref name="filter"/>. Thành phần của từng combo được nạp bằng một truy vấn riêng.
    /// </summary>
    Task<(IReadOnlyList<ProductBundleListRow> Items, int TotalCount)> GetPagedActiveAsync(
        ProductBundleFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thành phần của MỌI combo hiển thị khớp bộ lọc (bỏ qua phân trang), dùng để tính tồn khả dụng khi lọc "chỉ còn hàng"
    /// trước khi phân trang.
    /// </summary>
    Task<List<ProductBundleComponentRow>> GetActiveComponentsAsync(
        ProductBundleFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết combo Active chưa xoá (thành phần kèm ảnh + bậc giá); null nếu không tồn tại / ngừng bán.</summary>
    Task<ProductBundleDetailRow?> GetActiveDetailAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách combo chưa xoá cho quản trị (mọi trạng thái), lọc tên / trạng thái / danh mục, mới tạo trước.</summary>
    Task<(IReadOnlyList<AdminBundleRow> Items, int TotalCount)> GetAdminPagedAsync(
        string? search, string? status, Guid? categoryId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);
}
