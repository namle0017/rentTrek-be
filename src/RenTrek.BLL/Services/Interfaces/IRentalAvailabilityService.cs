using RenTrek.BLL.DTOs.Rental;
using RenTrek.BLL.DTOs.RentalAvailability;

namespace RenTrek.BLL.Services.Interfaces;

public interface IRentalAvailabilityService
{
    /// <summary>Kiểm tra khả dụng nhiều dòng thuê (API 4.6): validate khoảng thuê, nạp dòng, đánh giá theo nhu cầu cộng dồn.</summary>
    Task<CheckRentalAvailabilityResponse> CheckAsync(CheckRentalAvailabilityRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra hình dạng và nạp thông tin hàng hoá cho các dòng thuê.
    /// 400 INVALID_RENTAL_ITEM (cả hai / không có variant+bundle, số lượng &lt;= 0);
    /// 404 ITEM_NOT_FOUND / PRODUCT_NOT_FOUND / BUNDLE_NOT_FOUND.
    /// </summary>
    Task<List<ResolvedRentalLine>> ResolveLinesAsync(IReadOnlyList<RentalLineInput> lines, CancellationToken cancellationToken = default);

    /// <summary>Khả dụng từng dòng, tính theo tổng nhu cầu cộng dồn của cả giỏ (đã gồm đệm vệ sinh).</summary>
    Task<List<RentalLineAvailability>> EvaluateAsync(RentalPeriod period, IReadOnlyList<ResolvedRentalLine> lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chọn id tài sản cho từng biến thể. Gọi trong transaction của caller sau khi đã <see cref="LockVariantsAsync"/>;
    /// ném 409 RESOURCE_UNAVAILABLE nếu không đủ.
    /// </summary>
    Task<Dictionary<Guid, List<Guid>>> PickAssetsAsync(RentalPeriod period, IReadOnlyDictionary<Guid, int> demandByVariant, CancellationToken cancellationToken = default);

    /// <summary>Khoá advisory theo transaction cho các biến thể (BẮT BUỘC gọi trong transaction đang mở).</summary>
    Task LockVariantsAsync(IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>Thông báo thiếu hàng tiếng Việt, ngày theo Asia/Ho_Chi_Minh, ví dụ "Chỉ còn 1 Đệm hơi trong 24/10 – 26/10."; chuỗi rỗng khi dòng đủ hàng.</summary>
    string BuildLineMessage(RentalLineAvailability line, RentalPeriod period);
}
