using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.DTOs.AdminCatalog.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Quản lý tour màn O06/O07 (03-API-CONTRACT.md §6, FR-T01–T05): thông tin chung, ảnh, lịch trình, đồ tự mang,
/// đồ cấp và phiên bản. Tour đã có khách đặt thì khóa lịch trình / đồ; muốn đổi phải clone phiên bản mới.
/// </summary>
public interface IAdminTourService
{
    /// <summary>6.2. Tạo tour Draft, họ tour mới, version 1; trả id.</summary>
    Task<Guid> CreateTourAsync(SaveTourRequest request, CancellationToken cancellationToken);

    /// <summary>6.3. Thông tin chung kèm cờ khóa lịch trình.</summary>
    Task<AdminTourDetailResponse> GetTourAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.4. Sửa thông tin chung; khi đã khóa thì không được đổi số ngày.</summary>
    Task UpdateTourAsync(Guid tourId, SaveTourRequest request, CancellationToken cancellationToken);

    /// <summary>6.5. Xuất bản: cần ảnh đại diện, ít nhất 1 hoạt động và có đồ cấp hoặc đồ tự mang.</summary>
    Task PublishTourAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.6. Lưu trữ (ẩn) tour; chuyến cũ giữ nguyên.</summary>
    Task ArchiveTourAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>Xoá mềm cả họ tour (mọi phiên bản); chỉ khi chưa có chuyến nào, đã có chuyến thì lưu trữ.</summary>
    Task DeleteTourAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.7. Thư viện ảnh theo thứ tự hiển thị.</summary>
    Task<IReadOnlyList<AdminTourImageResponse>> GetImagesAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.8. Thêm ảnh; ảnh đại diện mới bỏ cờ đại diện của ảnh khác; trả id.</summary>
    Task<Guid> AddImageAsync(Guid tourId, SaveImageRequest request, CancellationToken cancellationToken);

    /// <summary>6.9. Sắp lại thứ tự ảnh (đủ mọi ảnh) và đặt ảnh đại diện.</summary>
    Task ReorderImagesAsync(Guid tourId, ReorderTourImagesRequest request, CancellationToken cancellationToken);

    /// <summary>6.10. Xoá mềm ảnh; xoá ảnh đại diện thì ảnh đầu tiên còn lại lên thay.</summary>
    Task DeleteImageAsync(Guid tourId, Guid imageId, CancellationToken cancellationToken);

    /// <summary>6.11. Lịch trình theo ngày, rồi theo thứ tự.</summary>
    Task<IReadOnlyList<AdminTourActivityResponse>> GetActivitiesAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.12. Thêm hoạt động (ngày trong số ngày tour); trả id. 400 ITINERARY_LOCKED nếu đã khóa.</summary>
    Task<Guid> AddActivityAsync(Guid tourId, SaveTourActivityRequest request, CancellationToken cancellationToken);

    /// <summary>6.13. Sửa hoạt động. 400 ITINERARY_LOCKED nếu đã khóa.</summary>
    Task UpdateActivityAsync(Guid tourId, Guid activityId, SaveTourActivityRequest request, CancellationToken cancellationToken);

    /// <summary>6.14. Xoá mềm hoạt động. 400 ITINERARY_LOCKED nếu đã khóa.</summary>
    Task DeleteActivityAsync(Guid tourId, Guid activityId, CancellationToken cancellationToken);

    /// <summary>6.15. Sắp lại thứ tự hoạt động trong một ngày (đủ mọi hoạt động của ngày).</summary>
    Task ReorderActivitiesAsync(Guid tourId, ReorderTourActivitiesRequest request, CancellationToken cancellationToken);

    /// <summary>6.16. Danh sách đồ khách tự mang.</summary>
    Task<IReadOnlyList<AdminTourPreparationItemResponse>> GetPreparationItemsAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.17. Thay toàn bộ đồ tự mang. 400 ITINERARY_LOCKED nếu đã khóa.</summary>
    Task SetPreparationItemsAsync(Guid tourId, TourPreparationItemsRequest request, CancellationToken cancellationToken);

    /// <summary>6.18. Định mức đồ cấp kèm SKU / tên sản phẩm / biến thể.</summary>
    Task<IReadOnlyList<AdminTourProvidedItemResponse>> GetProvidedItemsAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.19. Thêm định mức đồ cấp (mỗi biến thể một dòng); trả id. 400 ITINERARY_LOCKED nếu đã khóa.</summary>
    Task<Guid> AddProvidedItemAsync(Guid tourId, SaveTourProvidedItemRequest request, CancellationToken cancellationToken);

    /// <summary>6.20. Sửa định mức đồ cấp. 400 ITINERARY_LOCKED nếu đã khóa.</summary>
    Task UpdateProvidedItemAsync(Guid tourId, Guid itemId, SaveTourProvidedItemRequest request, CancellationToken cancellationToken);

    /// <summary>6.21. Xoá mềm định mức đồ cấp. 400 ITINERARY_LOCKED nếu đã khóa.</summary>
    Task DeleteProvidedItemAsync(Guid tourId, Guid itemId, CancellationToken cancellationToken);

    /// <summary>6.22. Bài viết cẩm nang gắn tour (bỏ bài đã xoá), theo thứ tự đọc.</summary>
    Task<IReadOnlyList<AdminTourPostResponse>> GetPostsAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.23. Thay toàn bộ bài viết gắn tour và thứ tự đọc; không bị khóa lịch trình chặn.</summary>
    Task SetPostsAsync(Guid tourId, TourPostsRequest request, CancellationToken cancellationToken);

    /// <summary>6.23a. Combo thuê gợi ý của tour, theo thứ tự hiển thị.</summary>
    Task<IReadOnlyList<AdminTourSuggestedBundleResponse>> GetSuggestedBundlesAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.23b. Thay toàn bộ combo gợi ý của tour; combo phải đang bán.</summary>
    Task SetSuggestedBundlesAsync(Guid tourId, TourSuggestedBundlesRequest request, CancellationToken cancellationToken);

    /// <summary>6.24. Mọi phiên bản trong họ tour, mới nhất trước.</summary>
    Task<IReadOnlyList<AdminTourVersionResponse>> GetVersionsAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.25. Clone toàn bộ cây tour sang version lớn nhất + 1 (Draft, chưa khóa); trả id bản mới.</summary>
    Task<Guid> CloneVersionAsync(Guid tourId, CancellationToken cancellationToken);

    /// <summary>6.26. So sánh hai phiên bản cùng họ: thông tin chung, lịch trình, đồ cấp, đồ tự mang, bài viết.</summary>
    Task<AdminTourDiffResponse> DiffVersionsAsync(Guid tourId, Guid compareWithVersionId, CancellationToken cancellationToken);
}
