using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

public class AssetService : IAssetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public AssetService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<AssetResponse>> GetPagedAssetsAsync(AssetQueryRequest request, CancellationToken cancellationToken = default)
    {
        var status = !string.IsNullOrWhiteSpace(request.AssetStatus) ? request.AssetStatus : request.Status;
        var (items, totalCount) = await _unitOfWork.EquipmentAssets.GetPagedAssetsAsync(
            request.Search,
            request.ProductVariantId,
            status,
            request.PageNumber,
            request.PageSize,
            request.SortBy,
            request.IsDescending,
            cancellationToken);

        var mapped = _mapper.Map<List<AssetResponse>>(items);

        return new PagedResponse<AssetResponse>
        {
            Items = mapped,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalItems = totalCount
        };
    }

    public async Task<IReadOnlyList<AssetResponse>> BulkCreateAssetsAsync(BulkCreateAssetsRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _unitOfWork.ProductVariants.GetByIdAsync(request.ProductVariantId, cancellationToken);
        if (variant is null || variant.IsDeleted)
        {
            throw new NotFoundException($"Không tìm thấy biến thể sản phẩm với id '{request.ProductVariantId}'.");
        }

        var prefix = request.CodePrefix.Trim().ToUpperInvariant();
        var baseCode = await _unitOfWork.EquipmentAssets.GenerateNextAssetCodeAsync(prefix, cancellationToken);

        // Parse starting sequence
        int startSeq = 1;
        if (baseCode.Length >= prefix.Length)
        {
            var match = Regex.Match(baseCode.Substring(prefix.Length), @"\d+");
            if (match.Success && int.TryParse(match.Value, out var seq))
            {
                startSeq = seq;
            }
        }

        var newAssets = new List<EquipmentAsset>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < request.Quantity; i++)
        {
            var code = $"{prefix}{(startSeq + i):D4}";
            newAssets.Add(new EquipmentAsset
            {
                EquipmentAssetId = Guid.NewGuid(),
                ProductVariantId = request.ProductVariantId,
                AssetCode = code,
                AssetStatus = EquipmentAssetStatuses.Available,
                ConditionNote = request.InitialConditionNote,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            });
        }

        await _unitOfWork.EquipmentAssets.AddRangeAsync(newAssets, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch created with variant info for mapping
        var created = new List<AssetResponse>();
        foreach (var item in newAssets)
        {
            item.ProductVariant = variant;
            created.Add(_mapper.Map<AssetResponse>(item));
        }

        return created;
    }

    public async Task<AssetDetailResponse> GetAssetByCodeAsync(string assetCode, CancellationToken cancellationToken = default)
    {
        var asset = await _unitOfWork.EquipmentAssets.GetByCodeWithDetailsAsync(assetCode, cancellationToken);
        if (asset is null)
        {
            throw new NotFoundException($"Không tìm thấy thiết bị với mã '{assetCode}'.");
        }

        return _mapper.Map<AssetDetailResponse>(asset);
    }

    public async Task<AssetDetailResponse> UpdateAssetStatusAsync(string assetCode, UpdateAssetStatusRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await _unitOfWork.EquipmentAssets.GetByCodeWithDetailsAsync(assetCode, cancellationToken);
        if (asset is null)
        {
            throw new NotFoundException($"Không tìm thấy thiết bị với mã '{assetCode}'.");
        }

        var newStatus = !string.IsNullOrWhiteSpace(request.NewStatus) ? request.NewStatus : request.AssetStatus;
        if (string.IsNullOrWhiteSpace(newStatus))
        {
            throw new ValidationException("Trạng thái mới không được để trống.");
        }

        asset.AssetStatus = newStatus.Trim();
        if (!string.IsNullOrWhiteSpace(request.ConditionNote))
        {
            asset.ConditionNote = request.ConditionNote.Trim();
        }
        asset.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.EquipmentAssets.Update(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AssetDetailResponse>(asset);
    }

    public async Task<IReadOnlyList<AssetAssignmentHistoryResponse>> GetAssetHistoryAsync(string assetCode, CancellationToken cancellationToken = default)
    {
        var asset = await _unitOfWork.EquipmentAssets.GetByCodeWithDetailsAsync(assetCode, cancellationToken);
        if (asset is null)
        {
            throw new NotFoundException($"Không tìm thấy thiết bị với mã '{assetCode}'.");
        }

        var history = asset.AssetAssignments
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AssetAssignmentHistoryResponse
            {
                AssignmentId = x.AssetAssignmentId,
                SourceType = x.RentalOrderItemId.HasValue ? "RentalOrder" :
                             x.TripProvidedItemId.HasValue ? "TripProvided" :
                             x.AppliedPromotionId.HasValue ? "Promotion" : "Other",
                SourceLabel = x.RentalOrderItem?.RentalOrder?.Order?.OrderCode ??
                             (x.TripProvidedItem != null ? $"{x.TripProvidedItem.Trip.Tour.Name} ({x.TripProvidedItem.Trip.StartAt:dd/MM})" :
                             x.AppliedPromotion?.PromotionTier?.Promotion?.Name ?? "Phân bổ hệ thống"),
                ReservedFrom = x.ReservedFrom,
                ReservedUntil = x.ReservedUntil,
                AssignedAt = x.AssignedAt,
                ReturnedAt = x.ReturnedAt,
                ReturnCondition = x.ReturnCondition,
                Status = x.Status
            })
            .ToList();

        return history;
    }

    public async Task<AssetGanttResponse> GetGanttScheduleAsync(AssetGanttQueryRequest request, CancellationToken cancellationToken = default)
    {
        var fromDate = request.From.Date;
        var toDate = request.To.Date;

        var assets = await _unitOfWork.EquipmentAssets.GetAssetsForGanttAsync(
            fromDate,
            toDate.AddDays(1),
            request.CategoryId,
            request.ProductVariantId,
            cancellationToken);

        var variantGroups = new List<GanttVariantGroupResponse>();
        var groupedByVariant = assets.GroupBy(a => a.ProductVariantId);

        var totalDays = (int)(toDate - fromDate).TotalDays + 1;
        var dateRange = Enumerable.Range(0, Math.Max(1, totalDays))
            .Select(offset => fromDate.AddDays(offset))
            .ToList();

        foreach (var group in groupedByVariant)
        {
            var firstAsset = group.First();
            var variant = firstAsset.ProductVariant;
            var variantAssets = group.ToList();

            // Daily summary for this variant
            var dailySummaries = new List<GanttDailySummaryResponse>();
            foreach (var day in dateRange)
            {
                var nextDay = day.AddDays(1);
                var totalCount = variantAssets.Count;
                var inUseCount = variantAssets.Count(a => a.AssetAssignments.Any(assign =>
                    assign.ReservedFrom < nextDay && assign.ReservedUntil > day &&
                    (assign.Status == AssignmentStatuses.Reserved || assign.Status == AssignmentStatuses.Issued)));

                var availableCount = Math.Max(0, totalCount - inUseCount);
                dailySummaries.Add(new GanttDailySummaryResponse
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    TotalCount = totalCount,
                    InUseCount = inUseCount,
                    AvailableCount = availableCount,
                    IsOverCapacity = inUseCount > totalCount
                });
            }

            // Asset items & timeline blocks
            var assetItems = new List<GanttAssetItemResponse>();
            foreach (var asset in variantAssets)
            {
                var blocks = asset.AssetAssignments
                    .Where(x => x.ReservedFrom < toDate.AddDays(1) && x.ReservedUntil > fromDate)
                    .OrderBy(x => x.ReservedFrom)
                    .Select(x => new GanttTimelineBlockResponse
                    {
                        AssignmentId = x.AssetAssignmentId,
                        From = x.ReservedFrom,
                        To = x.ReservedUntil,
                        Status = x.Status,
                        SourceType = x.RentalOrderItemId.HasValue ? "RentalOrder" :
                                     x.TripProvidedItemId.HasValue ? "TripProvided" :
                                     x.AppliedPromotionId.HasValue ? "Promotion" : "Other",
                        SourceLabel = x.RentalOrderItem?.RentalOrder?.Order?.OrderCode ??
                                     (x.TripProvidedItem != null ? $"{x.TripProvidedItem.Trip.Tour.Name}" :
                                     x.AppliedPromotion?.PromotionTier?.Promotion?.Name ?? "Phân bổ")
                    })
                    .ToList();

                assetItems.Add(new GanttAssetItemResponse
                {
                    AssetId = asset.EquipmentAssetId,
                    AssetCode = asset.AssetCode,
                    AssetStatus = asset.AssetStatus,
                    Timeline = blocks
                });
            }

            variantGroups.Add(new GanttVariantGroupResponse
            {
                VariantId = variant.Id,
                VariantName = variant.Name,
                ProductName = variant.Product?.Name ?? string.Empty,
                Sku = variant.Sku,
                TotalAssets = variantAssets.Count,
                DailySummaries = dailySummaries,
                Assets = assetItems
            });
        }

        return new AssetGanttResponse
        {
            From = fromDate,
            To = toDate,
            Variants = variantGroups
        };
    }

    public async Task<IReadOnlyList<AssetVariantOptionResponse>> GetProductVariantsAsync(CancellationToken cancellationToken = default)
    {
        var variants = await _unitOfWork.ProductVariants.Query()
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => !v.IsDeleted && !v.Product.IsDeleted)
            .OrderBy(v => v.Product.Name)
            .ThenBy(v => v.Name)
            .Select(v => new AssetVariantOptionResponse
            {
                Id = v.Id,
                ProductName = v.Product.Name,
                VariantName = v.Name,
                Sku = v.Sku
            })
            .ToListAsync(cancellationToken);

        return variants;
    }
}
