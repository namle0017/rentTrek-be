using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

public class TripProvidedItemService : ITripProvidedItemService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public TripProvidedItemService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>Staff chỉ được cấp/thu đồ cho chuyến mình phụ trách (PRD §3.2 "G"); vai trò khác bỏ qua để không tốn query.</summary>
    private async Task EnsureManagesTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        if (_currentUser.Role != RoleNames.Staff)
            return;
        TripGroupRules.EnsureManagesTrip(_currentUser, await _unitOfWork.TripGroups.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chuyến đi"));
    }

    public async Task<TripProvidedItemListResponse> GetProvidedItemsByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await _unitOfWork.Trips.GetTripWithTourAndRequirementsAsync(tripId, cancellationToken);
        if (trip == null)
        {
            throw new NotFoundException($"Chuyến đi với ID '{tripId}' không tồn tại.");
        }

        var totalParticipants = trip.BookingParticipants.Count(p => !p.IsDeleted);
        var totalGroups = trip.TripGroups.Count(g => !g.IsDeleted);

        // Đồng bộ danh mục đồ cấp cấu hình từ Tour nếu chuyến chưa sinh đủ
        var existingTourItemIds = trip.TripProvidedItems.Select(tpi => tpi.TourProvidedItemId).ToHashSet();
        var tourProvidedItems = trip.Tour.ProvidedItems.Where(tpi => tpi.ProductVariantId.HasValue).ToList();

        var hasNewItems = false;
        foreach (var tourItem in tourProvidedItems)
        {
            if (!existingTourItemIds.Contains(tourItem.Id))
            {
                var reqQty = CalculateRequiredQuantity(tourItem.QuantityBasis, tourItem.Quantity, totalParticipants, totalGroups);
                var newTripItem = new TripProvidedItem
                {
                    TripProvidedItemId = Guid.NewGuid(),
                    TripId = tripId,
                    TourProvidedItemId = tourItem.Id,
                    ProductVariantId = tourItem.ProductVariantId!.Value,
                    RequiredQuantity = reqQty,
                    Status = TripProvidedItemStatuses.Missing,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.TripProvidedItems.AddAsync(newTripItem, cancellationToken);
                hasNewItems = true;
            }
        }

        if (hasNewItems)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Đọc lại danh sách đồ cấp chi tiết sau khi đồng bộ
        var items = await _unitOfWork.TripProvidedItems.GetByTripIdWithDetailsAsync(tripId, cancellationToken);
        var hasShortage = false;
        var itemResponses = new List<TripProvidedItemDetailResponse>();

        foreach (var item in items)
        {
            var tourBasis = item.TourProvidedItem?.QuantityBasis ?? "PerPerson";
            var tourQty = item.TourProvidedItem?.Quantity ?? 1;
            var expectedReq = CalculateRequiredQuantity(tourBasis, tourQty, totalParticipants, totalGroups);

            // Cập nhật số lượng nếu định mức hoặc quy mô người/nhóm thay đổi
            if (item.RequiredQuantity != expectedReq && item.Status == TripProvidedItemStatuses.Missing)
            {
                item.RequiredQuantity = expectedReq;
            }

            var assignedActive = item.AssetAssignments
                .Where(a => a.Status == AssignmentStatuses.Reserved || a.Status == AssignmentStatuses.Issued)
                .ToList();
            var assignedCount = assignedActive.Count;
            var missingCount = Math.Max(0, item.RequiredQuantity - assignedCount);

            if (missingCount > 0)
            {
                hasShortage = true;
            }

            // Tính toán lại trạng thái item
            var newStatus = "Missing";
            if (item.AssetAssignments.Count > 0 && item.AssetAssignments.All(a => a.Status == AssignmentStatuses.Returned))
            {
                newStatus = "Recovered";
            }
            else if (assignedCount >= item.RequiredQuantity && item.RequiredQuantity > 0)
            {
                newStatus = "Ready";
            }

            item.Status = newStatus;

            itemResponses.Add(new TripProvidedItemDetailResponse
            {
                TripProvidedItemId = item.TripProvidedItemId,
                TourProvidedItemId = item.TourProvidedItemId,
                ProductVariantId = item.ProductVariantId,
                ProductName = item.ProductVariant?.Product?.Name ?? "Thiết bị",
                VariantName = item.ProductVariant?.Name ?? "Tiêu chuẩn",
                Sku = item.ProductVariant?.Sku ?? string.Empty,
                QuantityBasis = tourBasis,
                TourConfigQuantity = tourQty,
                RequiredQuantity = item.RequiredQuantity,
                AssignedCount = assignedCount,
                MissingCount = missingCount,
                PreparedByUserId = item.PreparedByUserId,
                PreparedByUserName = item.PreparedByUser?.FullName ?? item.PreparedByUser?.Email,
                Status = item.Status,
                AssignedAssets = item.AssetAssignments
                    .OrderBy(a => a.CreatedAt)
                    .Select(a => new TripAssignedAssetResponse
                    {
                        AssignmentId = a.AssetAssignmentId,
                        AssetId = a.EquipmentAssetId,
                        AssetCode = a.EquipmentAsset?.AssetCode ?? string.Empty,
                        Sku = a.EquipmentAsset?.ProductVariant?.Sku ?? string.Empty,
                        Status = a.Status,
                        AssignedAt = a.AssignedAt == default ? null : a.AssignedAt,
                        ReturnedAt = a.ReturnedAt,
                        ReturnCondition = a.ReturnCondition
                    })
                    .ToList()
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TripProvidedItemListResponse
        {
            TripId = trip.TripId,
            TourName = trip.Tour.Name,
            StartAt = trip.StartAt,
            EndAt = trip.EndAt,
            TotalParticipants = totalParticipants,
            TotalGroups = totalGroups,
            HasShortage = hasShortage,
            Items = itemResponses
        };
    }

    public async Task<List<AvailableAssetResponse>> GetAvailableAssetsForItemAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await _unitOfWork.TripProvidedItems.GetByIdWithDetailsAsync(tripId, itemId, cancellationToken);
        if (item == null)
        {
            throw new NotFoundException($"Không tìm thấy mục đồ cấp ID '{itemId}' của chuyến ID '{tripId}'.");
        }

        var available = await _unitOfWork.TripProvidedItems.GetAvailableAssetsForItemAsync(
            item.ProductVariantId,
            item.Trip.StartAt.UtcDateTime,
            item.Trip.EndAt.UtcDateTime,
            cancellationToken);

        return available.Select(ea => new AvailableAssetResponse
        {
            AssetId = ea.EquipmentAssetId,
            AssetCode = ea.AssetCode,
            Sku = ea.ProductVariant?.Sku ?? string.Empty,
            ConditionNote = ea.ConditionNote,
            AssetStatus = ea.AssetStatus
        }).ToList();
    }

    public async Task<bool> AssignAssetsToTripItemAsync(Guid tripId, Guid itemId, AssignTripAssetsRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        await EnsureManagesTripAsync(tripId, cancellationToken);
        var item = await _unitOfWork.TripProvidedItems.GetByIdWithDetailsAsync(tripId, itemId, cancellationToken);
        if (item == null)
        {
            throw new NotFoundException($"Không tìm thấy mục đồ cấp ID '{itemId}' của chuyến ID '{tripId}'.");
        }

        var cleanCodes = request.AssetCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (cleanCodes.Count == 0)
        {
            throw new ValidationException("assetCodes", "Danh sách mã thiết bị hợp lệ không được để trống.");
        }

        var availableAssets = await _unitOfWork.TripProvidedItems.GetAvailableAssetsForItemAsync(
            item.ProductVariantId,
            item.Trip.StartAt.UtcDateTime,
            item.Trip.EndAt.UtcDateTime,
            cancellationToken);

        var availableByCode = availableAssets.ToDictionary(a => a.AssetCode, StringComparer.OrdinalIgnoreCase);

        foreach (var code in cleanCodes)
        {
            if (!availableByCode.TryGetValue(code, out var asset))
            {
                throw new ValidationException("assetCodes", $"Thiết bị '{code}' không tồn tại, không thuộc biến thể này hoặc đang bận trong khung ngày của chuyến.");
            }

            var assignment = new AssetAssignment
            {
                AssetAssignmentId = Guid.NewGuid(),
                EquipmentAssetId = asset.EquipmentAssetId,
                TripProvidedItemId = itemId,
                Status = AssignmentStatuses.Reserved,
                ReservedFrom = item.Trip.StartAt.UtcDateTime,
                ReservedUntil = item.Trip.EndAt.UtcDateTime,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.AssetAssignments.AddAsync(assignment, cancellationToken);
        }

        item.PreparedByUserId = currentUserId;

        var totalAssigned = item.AssetAssignments.Count(a => a.Status == AssignmentStatuses.Reserved || a.Status == AssignmentStatuses.Issued) + cleanCodes.Count;
        if (totalAssigned >= item.RequiredQuantity && item.RequiredQuantity > 0)
        {
            item.Status = TripProvidedItemStatuses.Ready;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnassignAssetFromTripAsync(Guid tripId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await EnsureManagesTripAsync(tripId, cancellationToken);
        var assignment = await _unitOfWork.AssetAssignments.GetAssignmentWithAssetAndTripItemAsync(assignmentId, cancellationToken);
        if (assignment == null || assignment.TripProvidedItem == null || assignment.TripProvidedItem.TripId != tripId)
        {
            throw new NotFoundException($"Bản ghi gán thiết bị ID '{assignmentId}' không thuộc chuyến đi này.");
        }

        if (assignment.Status == AssignmentStatuses.Issued)
        {
            throw new ValidationException("assignmentId", "Không thể hủy gán thiết bị đã xuất kho giao cho đoàn. Vui lòng thực hiện quy trình thu hồi.");
        }

        _unitOfWork.AssetAssignments.Remove(assignment);

        var remainingAssigned = assignment.TripProvidedItem.AssetAssignments
            .Count(a => a.AssetAssignmentId != assignmentId && (a.Status == AssignmentStatuses.Reserved || a.Status == AssignmentStatuses.Issued));

        if (remainingAssigned < assignment.TripProvidedItem.RequiredQuantity)
        {
            assignment.TripProvidedItem.Status = TripProvidedItemStatuses.Missing;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> IssueTripGearAsync(Guid tripId, IssueTripGearRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureManagesTripAsync(tripId, cancellationToken);
        List<AssetAssignment> assignments;
        if (request.AssetCodes != null && request.AssetCodes.Count > 0)
        {
            assignments = await _unitOfWork.AssetAssignments.GetAssignmentsByAssetCodesAndTripIdAsync(tripId, request.AssetCodes, cancellationToken);
        }
        else
        {
            assignments = await _unitOfWork.AssetAssignments.GetAssignmentsByTripIdAsync(tripId, cancellationToken);
        }

        var reservedAssignments = assignments.Where(a => a.Status == AssignmentStatuses.Reserved).ToList();
        if (reservedAssignments.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var a in reservedAssignments)
        {
            a.Status = AssignmentStatuses.Issued;
            a.AssignedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return reservedAssignments.Count;
    }

    public async Task<int> ReturnTripGearAsync(Guid tripId, ReturnTripGearRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureManagesTripAsync(tripId, cancellationToken);
        var returnCodes = request.Returns.Select(r => r.AssetCode.Trim().ToUpperInvariant()).ToList();
        var assignments = await _unitOfWork.AssetAssignments.GetAssignmentsByAssetCodesAndTripIdAsync(tripId, returnCodes, cancellationToken);

        var returnMap = request.Returns.ToDictionary(r => r.AssetCode.Trim().ToUpperInvariant(), r => r, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var updatedCount = 0;

        foreach (var a in assignments.Where(a => a.Status == AssignmentStatuses.Issued || a.Status == AssignmentStatuses.Reserved))
        {
            if (returnMap.TryGetValue(a.EquipmentAsset.AssetCode, out var retInfo))
            {
                a.Status = AssignmentStatuses.Returned;
                a.ReturnedAt = now;
                a.ReturnCondition = retInfo.ReturnCondition;

                if (string.Equals(retInfo.ReturnCondition, "Damaged", StringComparison.OrdinalIgnoreCase))
                {
                    a.EquipmentAsset.AssetStatus = EquipmentAssetStatuses.Damaged;
                }
                else if (string.Equals(retInfo.ReturnCondition, "Lost", StringComparison.OrdinalIgnoreCase))
                {
                    a.EquipmentAsset.AssetStatus = EquipmentAssetStatuses.Lost;
                }

                if (!string.IsNullOrWhiteSpace(retInfo.Note))
                {
                    a.EquipmentAsset.ConditionNote = retInfo.Note.Trim();
                }

                updatedCount++;
            }
        }

        // Kiểm tra nếu tất cả đồ của chuyến đã trả xong, đổi status sang Recovered
        var allTripItems = await _unitOfWork.TripProvidedItems.GetByTripIdWithDetailsAsync(tripId, cancellationToken);
        foreach (var item in allTripItems)
        {
            if (item.AssetAssignments.Count > 0 && item.AssetAssignments.All(a => a.Status == AssignmentStatuses.Returned))
            {
                item.Status = TripProvidedItemStatuses.Recovered;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return updatedCount;
    }

    private static int CalculateRequiredQuantity(string basis, int qty, int totalParticipants, int totalGroups)
    {
        if (string.Equals(basis, "PerPerson", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(1, totalParticipants) * qty;
        }

        if (string.Equals(basis, "PerGroup", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(1, totalGroups) * qty;
        }

        return Math.Max(1, qty);
    }
}
