using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;

namespace RenTrek.BLL.Services.Warehouse.Interfaces;

public interface IAssetService
{
    Task<PagedResponse<AssetResponse>> GetPagedAssetsAsync(AssetQueryRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetResponse>> BulkCreateAssetsAsync(BulkCreateAssetsRequest request, CancellationToken cancellationToken = default);

    Task<AssetDetailResponse> GetAssetByCodeAsync(string assetCode, CancellationToken cancellationToken = default);

    Task<AssetDetailResponse> UpdateAssetStatusAsync(string assetCode, UpdateAssetStatusRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetAssignmentHistoryResponse>> GetAssetHistoryAsync(string assetCode, CancellationToken cancellationToken = default);

    Task<AssetGanttResponse> GetGanttScheduleAsync(AssetGanttQueryRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetVariantOptionResponse>> GetProductVariantsAsync(CancellationToken cancellationToken = default);
}
