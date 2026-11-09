using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Chương trình khuyến mãi, bậc giảm giá và lịch sử sử dụng.</summary>
public interface IAdminPromotionService
{
    Task<PagedResponse<AdminPromotionResponse>> GetPromotionsAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<AdminPromotionDetailResponse> GetPromotionAsync(Guid id, CancellationToken cancellationToken);
    Task<Guid> CreatePromotionAsync(CreatePromotionRequest request, CancellationToken cancellationToken);
    Task<Guid> UpdatePromotionAsync(Guid id, UpdatePromotionRequest request, CancellationToken cancellationToken);
    Task SetPromotionStatusAsync(Guid id, StatusRequest request, CancellationToken cancellationToken);
    Task SetPromotionTiersAsync(Guid id, PromotionTiersRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<AdminPromotionUsageResponse>> GetPromotionUsageAsync(Guid id, AdminQueryRequest request, CancellationToken cancellationToken);
}
