using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Ghi danh mục cho thuê: sản phẩm, biến thể, bảng giá, combo (03-API-CONTRACT.md §13, FR-P01/P04/P05/P07).
/// Xoá luôn là xoá mềm để đơn cũ vẫn đọc được lịch sử.
/// </summary>
public interface IAdminProductCatalogService
{
    /// <summary>Tạo sản phẩm cha; trả id.</summary>
    Task<Guid> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa thông tin sản phẩm cha.</summary>
    Task UpdateProductAsync(Guid productId, SaveProductRequest request, CancellationToken cancellationToken);

    /// <summary>Ẩn sản phẩm (xoá mềm); biến thể của nó tự ẩn theo.</summary>
    Task DeleteProductAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Thêm biến thể cho sản phẩm; 409 SKU_ALREADY_EXISTS nếu trùng SKU. Trả id.</summary>
    Task<Guid> CreateVariantAsync(Guid productId, SaveVariantRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa biến thể; 409 SKU_ALREADY_EXISTS nếu trùng SKU biến thể khác.</summary>
    Task UpdateVariantAsync(Guid variantId, SaveVariantRequest request, CancellationToken cancellationToken);

    /// <summary>Ẩn biến thể (xoá mềm).</summary>
    Task DeleteVariantAsync(Guid variantId, CancellationToken cancellationToken);

    /// <summary>Bảng bậc giá của biến thể, sắp theo MinDays.</summary>
    Task<IReadOnlyList<AdminPriceTierItemResponse>> GetVariantPriceTiersAsync(Guid variantId, CancellationToken cancellationToken);

    /// <summary>Thay toàn bộ bảng bậc giá của biến thể (đơn đã chốt giữ giá snapshot).</summary>
    Task SetVariantPriceTiersAsync(Guid variantId, PriceTiersRequest request, CancellationToken cancellationToken);

    /// <summary>13.9. Thêm ảnh biến thể; ảnh đầu tiên hoặc ảnh đánh dấu đại diện sẽ thay ảnh đại diện cũ. Trả id.</summary>
    Task<Guid> AddVariantImageAsync(Guid variantId, SaveImageRequest request, CancellationToken cancellationToken);

    /// <summary>13.10. Xoá mềm ảnh biến thể; xoá ảnh đại diện thì ảnh đứng đầu còn lại lên thay.</summary>
    Task DeleteVariantImageAsync(Guid variantId, Guid imageId, CancellationToken cancellationToken);

    /// <summary>Đặt ảnh có sẵn của biến thể làm ảnh đại diện (hạ cờ đại diện ở các ảnh khác).</summary>
    Task SetPrimaryVariantImageAsync(Guid variantId, Guid imageId, CancellationToken cancellationToken);

    /// <summary>Bảng giá tổng hợp mọi biến thể và combo, lọc theo danh mục nếu có.</summary>
    Task<IReadOnlyList<AdminPricingItemResponse>> GetPricingAsync(Guid? categoryId, CancellationToken cancellationToken);

    /// <summary>Danh sách combo cho quản trị (dùng Search, Status, CategoryId, phân trang).</summary>
    Task<PagedResponse<AdminBundleListItemResponse>> GetBundlesAsync(AdminQueryRequest request, CancellationToken cancellationToken);

    /// <summary>Chi tiết combo: thông tin, thành phần, bậc giá.</summary>
    Task<AdminBundleDetailResponse> GetBundleAsync(Guid bundleId, CancellationToken cancellationToken);

    /// <summary>Tạo combo; không cho tạo thẳng Active vì chưa có thành phần. Trả id.</summary>
    Task<Guid> CreateBundleAsync(SaveBundleRequest request, CancellationToken cancellationToken);

    /// <summary>Sửa combo; chuyển Active cần ít nhất 1 thành phần.</summary>
    Task UpdateBundleAsync(Guid bundleId, SaveBundleRequest request, CancellationToken cancellationToken);

    /// <summary>Ẩn combo (xoá mềm).</summary>
    Task DeleteBundleAsync(Guid bundleId, CancellationToken cancellationToken);

    /// <summary>Thay toàn bộ thành phần combo; mọi biến thể phải đang bán.</summary>
    Task SetBundleItemsAsync(Guid bundleId, BundleItemsRequest request, CancellationToken cancellationToken);

    /// <summary>Bảng bậc giá của combo, sắp theo MinDays.</summary>
    Task<IReadOnlyList<AdminPriceTierItemResponse>> GetBundlePriceTiersAsync(Guid bundleId, CancellationToken cancellationToken);

    /// <summary>Thay toàn bộ bảng bậc giá của combo.</summary>
    Task SetBundlePriceTiersAsync(Guid bundleId, PriceTiersRequest request, CancellationToken cancellationToken);
}
