using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Warehouse.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Warehouse.Implementations;

public class FulfillmentService : IFulfillmentService
{
    private readonly IUnitOfWork _unitOfWork;

    public FulfillmentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<HandoverChecklistResponse> GetHandoverChecklistAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var checklines = new List<HandoverChecklineItemResponse>();
        var totalRequired = 0;
        var totalScanned = 0;

        foreach (var item in rentalOrder.RentalOrderItems)
        {
            var scannedAssets = item.AssetAssignments
                .Where(a => a.Status == "Issued" || a.Status == "Reserved")
                .OrderBy(a => a.CreatedAt)
                .Select(a => new HandoverScannedAssetResponse
                {
                    AssignmentId = a.AssetAssignmentId,
                    AssetCode = a.EquipmentAsset?.AssetCode ?? string.Empty,
                    Sku = a.EquipmentAsset?.ProductVariant?.Sku ?? string.Empty,
                    Status = a.Status,
                    ScannedAt = a.AssignedAt == default ? null : a.AssignedAt
                })
                .ToList();

            var scannedCount = item.AssetAssignments.Count(a => a.Status == "Issued");
            totalRequired += item.Quantity;
            totalScanned += scannedCount;

            checklines.Add(new HandoverChecklineItemResponse
            {
                RentalOrderItemId = item.RentalOrderItemId,
                ProductVariantId = item.ProductVariantId,
                ProductName = item.ProductVariant?.Product?.Name ?? item.Bundle?.Name ?? "Thiết bị thuê",
                VariantName = item.ProductVariant?.Name ?? "Tiêu chuẩn",
                Sku = item.ProductVariant?.Sku ?? string.Empty,
                RequiredQuantity = item.Quantity,
                ScannedQuantity = scannedCount,
                ScannedAssets = scannedAssets
            });
        }

        return new HandoverChecklistResponse
        {
            OrderCode = rentalOrder.Order.OrderCode,
            CustomerName = rentalOrder.Order.CustomerUser.FullName,
            CustomerPhone = rentalOrder.Order.CustomerUser.Phone,
            OrderStatus = rentalOrder.Order.Status,
            RentalStartAt = rentalOrder.RentalStartAt,
            RentalEndAt = rentalOrder.RentalEndAt,
            TotalRequiredItems = totalRequired,
            TotalScannedItems = totalScanned,
            IsReadyToComplete = totalScanned >= totalRequired && totalRequired > 0,
            Items = checklines
        };
    }

    public async Task<HandoverScanResultResponse> ScanHandoverAssetAsync(string orderCode, HandoverScanRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var cleanCode = request.AssetCode.Trim().ToUpper();
        var asset = await _unitOfWork.EquipmentAssets.GetByCodeWithDetailsAsync(cleanCode, cancellationToken);
        if (asset == null)
        {
            throw new NotFoundException($"Mã thiết bị '{cleanCode}' không tồn tại trong kho.");
        }

        if (asset.AssetStatus != "Available" && !asset.AssetAssignments.Any(a => a.RentalOrderItem != null && a.RentalOrderItem.RentalOrderId == rentalOrder.OrderId && a.Status == "Reserved"))
        {
            throw new ValidationException("assetCode", $"Thiết bị '{cleanCode}' đang ở tình trạng '{asset.AssetStatus}', không sẵn sàng để bàn giao.");
        }

        // Tìm dòng trong đơn thuê tương ứng với variant của thiết bị
        var matchingItem = rentalOrder.RentalOrderItems
            .FirstOrDefault(roi => roi.ProductVariantId == asset.ProductVariantId);

        if (matchingItem == null)
        {
            throw new ValidationException("assetCode", $"Thiết bị '{cleanCode}' (SKU: {asset.ProductVariant?.Sku}) không thuộc bất kỳ món đồ nào trong đơn thuê '{orderCode}'.");
        }

        // Kiểm tra xem thiết bị này đã được bàn giao (Issued) chưa
        var alreadyIssued = matchingItem.AssetAssignments
            .Any(a => a.EquipmentAssetId == asset.EquipmentAssetId && a.Status == "Issued");

        if (alreadyIssued)
        {
            throw new ValidationException("assetCode", $"Thiết bị '{cleanCode}' đã được quét bàn giao cho đơn hàng này rồi.");
        }

        // Nếu đã có bản ghi Reserved trước đó, chuyển sang Issued
        var existingAssignment = matchingItem.AssetAssignments
            .FirstOrDefault(a => a.EquipmentAssetId == asset.EquipmentAssetId && a.Status == "Reserved");

        var now = DateTime.UtcNow;
        if (existingAssignment != null)
        {
            existingAssignment.Status = "Issued";
            existingAssignment.AssignedAt = now;
        }
        else
        {
            // Kiểm tra số lượng đã quét đủ chưa
            var currentScanned = matchingItem.AssetAssignments.Count(a => a.Status == "Issued");
            if (currentScanned >= matchingItem.Quantity)
            {
                throw new ValidationException("assetCode", $"Món '{matchingItem.ProductVariant?.Product?.Name}' đã quét đủ số lượng ({matchingItem.Quantity} cái).");
            }

            var newAssignment = new AssetAssignment
            {
                AssetAssignmentId = Guid.NewGuid(),
                EquipmentAssetId = asset.EquipmentAssetId,
                RentalOrderItemId = matchingItem.RentalOrderItemId,
                Status = "Issued",
                AssignedAt = now,
                ReservedFrom = rentalOrder.RentalStartAt,
                ReservedUntil = rentalOrder.RentalEndAt,
                CreatedAt = now
            };

            await _unitOfWork.AssetAssignments.AddAsync(newAssignment, cancellationToken);
        }

        rentalOrder.HandledByUserId = currentUserId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Tính tiến độ sau khi quét
        var totalReq = rentalOrder.RentalOrderItems.Sum(i => i.Quantity);
        var totalScan = rentalOrder.RentalOrderItems.SelectMany(i => i.AssetAssignments).Count(a => a.Status == "Issued");

        return new HandoverScanResultResponse
        {
            AssetCode = asset.AssetCode,
            Sku = asset.ProductVariant?.Sku ?? string.Empty,
            ProductName = asset.ProductVariant?.Product?.Name ?? "Thiết bị",
            ScannedAt = now,
            Progress = new HandoverProgressResponse
            {
                ScannedItems = totalScan,
                TotalRequiredItems = totalReq,
                IsCompleted = totalScan >= totalReq && totalReq > 0
            }
        };
    }

    public async Task<bool> CompleteHandoverAsync(string orderCode, CompleteHandoverRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var totalReq = rentalOrder.RentalOrderItems.Sum(i => i.Quantity);
        var totalScan = rentalOrder.RentalOrderItems.SelectMany(i => i.AssetAssignments).Count(a => a.Status == "Issued");

        if (totalScan < totalReq && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ValidationException("note", $"Chưa quét đủ thiết bị ({totalScan}/{totalReq}). Vui lòng nhập lý do giải trình để hoàn tất bàn giao.");
        }

        rentalOrder.Order.Status = "InFulfillment";
        rentalOrder.Order.UpdatedAt = DateTimeOffset.UtcNow;
        rentalOrder.HandledByUserId = currentUserId;

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            rentalOrder.Order.Note = string.IsNullOrWhiteSpace(rentalOrder.Order.Note)
                ? $"[Bàn giao thiếu]: {request.Note}"
                : $"{rentalOrder.Order.Note} | [Bàn giao thiếu]: {request.Note}";
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ReturnChecklistResponse> GetReturnChecklistAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var allAssignments = rentalOrder.RentalOrderItems
            .SelectMany(i => i.AssetAssignments)
            .OrderBy(a => a.CreatedAt)
            .ToList();

        var now = DateTime.UtcNow;
        var isOverdue = now > rentalOrder.RentalEndAt;
        var overdueDays = isOverdue ? (int)Math.Ceiling((now - rentalOrder.RentalEndAt).TotalDays) : 0;

        var items = allAssignments.Select(a => new ReturnChecklistItemResponse
        {
            AssignmentId = a.AssetAssignmentId,
            AssetCode = a.EquipmentAsset?.AssetCode ?? string.Empty,
            Sku = a.EquipmentAsset?.ProductVariant?.Sku ?? string.Empty,
            ProductName = a.EquipmentAsset?.ProductVariant?.Product?.Name ?? "Thiết bị",
            VariantName = a.EquipmentAsset?.ProductVariant?.Name ?? "Tiêu chuẩn",
            Status = a.Status,
            AssignedAt = a.AssignedAt == default ? null : a.AssignedAt,
            ReturnedAt = a.ReturnedAt,
            ReturnCondition = a.ReturnCondition
        }).ToList();

        var totalIssued = allAssignments.Count(a => a.Status == "Issued");
        var totalReturned = allAssignments.Count(a => a.Status == "Returned");
        var totalLost = allAssignments.Count(a => a.Status == "LostOrException");

        return new ReturnChecklistResponse
        {
            OrderCode = rentalOrder.Order.OrderCode,
            CustomerName = rentalOrder.Order.CustomerUser.FullName,
            CustomerPhone = rentalOrder.Order.CustomerUser.Phone,
            OrderStatus = rentalOrder.Order.Status,
            RentalStartAt = rentalOrder.RentalStartAt,
            RentalEndAt = rentalOrder.RentalEndAt,
            IsOverdue = isOverdue,
            OverdueDays = overdueDays,
            TotalIssuedItems = totalIssued,
            TotalReturnedItems = totalReturned,
            TotalLostItems = totalLost,
            IsAllReturned = totalIssued == 0 && (totalReturned + totalLost) > 0,
            Items = items
        };
    }

    public async Task<ReturnScanResultResponse> ScanReturnAssetAsync(string orderCode, ReturnScanRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var cleanCode = request.AssetCode.Trim().ToUpper();
        var assignment = rentalOrder.RentalOrderItems
            .SelectMany(i => i.AssetAssignments)
            .FirstOrDefault(a => string.Equals(a.EquipmentAsset?.AssetCode, cleanCode, StringComparison.OrdinalIgnoreCase)
                                 && (a.Status == "Issued" || a.Status == "Reserved"));

        if (assignment == null)
        {
            throw new ValidationException("assetCode", $"Thiết bị '{cleanCode}' không nằm trong danh sách đang bàn giao của đơn hàng '{orderCode}'.");
        }

        var now = DateTime.UtcNow;
        assignment.Status = "Returned";
        assignment.ReturnedAt = now;
        assignment.ReturnCondition = request.ReturnCondition;

        // Cập nhật tình trạng thiết bị vật lý
        if (string.Equals(request.ReturnCondition, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            assignment.EquipmentAsset.AssetStatus = "Available";
        }
        else if (string.Equals(request.ReturnCondition, "Damaged", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(request.ReturnCondition, "MissingAccessory", StringComparison.OrdinalIgnoreCase))
        {
            assignment.EquipmentAsset.AssetStatus = "Damaged";
        }

        if (!string.IsNullOrWhiteSpace(request.ConditionNote))
        {
            assignment.EquipmentAsset.ConditionNote = request.ConditionNote.Trim();
        }

        // Kiểm tra trễ hạn
        var isOverdue = now > rentalOrder.RentalEndAt;
        var overdueDays = isOverdue ? (int)Math.Ceiling((now - rentalOrder.RentalEndAt).TotalDays) : 0;
        decimal? suggestedLateFee = null;

        if (isOverdue && overdueDays > 0)
        {
            suggestedLateFee = overdueDays * 100000m;
        }

        Guid? damageChargeId = null;
        if (request.AutoCreateDamageCharge && request.SuggestedDamageAmount.HasValue && request.SuggestedDamageAmount.Value > 0)
        {
            var charge = new RentalCharge
            {
                RentalChargeId = Guid.NewGuid(),
                RentalOrderId = rentalOrder.OrderId,
                EquipmentAssetId = assignment.EquipmentAssetId,
                ChargeType = "Damage",
                Amount = request.SuggestedDamageAmount.Value,
                Reason = request.ConditionNote ?? "Hư hỏng thiết bị khi kiểm tra nhận trả đồ",
                Status = "Draft",
                CreatedByUserId = currentUserId,
                CreatedAt = now
            };

            await _unitOfWork.RentalCharges.AddAsync(charge, cancellationToken);
            damageChargeId = charge.RentalChargeId;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReturnScanResultResponse
        {
            AssetCode = assignment.EquipmentAsset.AssetCode,
            Sku = assignment.EquipmentAsset.ProductVariant?.Sku ?? string.Empty,
            ReturnCondition = request.ReturnCondition,
            ConditionNote = request.ConditionNote,
            IsOverdue = isOverdue,
            OverdueDays = overdueDays,
            SuggestedLateFee = suggestedLateFee,
            CreatedDamageChargeId = damageChargeId,
            Status = "Returned"
        };
    }

    public async Task<bool> ReportLostAssetAsync(string orderCode, ReportLostAssetRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var cleanCode = request.AssetCode.Trim().ToUpper();
        var assignment = rentalOrder.RentalOrderItems
            .SelectMany(i => i.AssetAssignments)
            .FirstOrDefault(a => string.Equals(a.EquipmentAsset?.AssetCode, cleanCode, StringComparison.OrdinalIgnoreCase)
                                 && (a.Status == "Issued" || a.Status == "Reserved"));

        if (assignment == null)
        {
            throw new ValidationException("assetCode", $"Thiết bị '{cleanCode}' không nằm trong danh sách đang bàn giao của đơn hàng '{orderCode}'.");
        }

        var now = DateTime.UtcNow;
        assignment.Status = "LostOrException";
        assignment.ReturnedAt = now;
        assignment.ReturnCondition = "Lost";
        assignment.EquipmentAsset.AssetStatus = "Lost";

        var lossCharge = new RentalCharge
        {
            RentalChargeId = Guid.NewGuid(),
            RentalOrderId = rentalOrder.OrderId,
            EquipmentAssetId = assignment.EquipmentAssetId,
            ChargeType = "Loss",
            Amount = request.ChargeAmount ?? 500000m,
            Reason = request.Note ?? $"Báo mất thiết bị {cleanCode} trong quá trình thuê",
            Status = "Draft",
            CreatedByUserId = currentUserId,
            CreatedAt = now
        };

        await _unitOfWork.RentalCharges.AddAsync(lossCharge, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteReturnAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var activeAssignments = rentalOrder.RentalOrderItems
            .SelectMany(i => i.AssetAssignments)
            .Count(a => a.Status == "Issued");

        if (activeAssignments > 0)
        {
            throw new ValidationException("orderCode", $"Vẫn còn {activeAssignments} thiết bị chưa được quét thu hồi hoặc báo mất.");
        }

        return true;
    }

    public async Task<List<RentalChargeDetailResponse>> GetChargesAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        var charges = await _unitOfWork.RentalCharges.GetChargesByOrderCodeAsync(orderCode, cancellationToken);
        return charges.Select(c => new RentalChargeDetailResponse
        {
            ChargeId = c.RentalChargeId,
            OrderCode = orderCode,
            ChargeType = c.ChargeType,
            EquipmentAssetCode = c.EquipmentAsset?.AssetCode,
            ProductName = c.EquipmentAsset?.ProductVariant?.Product?.Name,
            Amount = c.Amount,
            Reason = c.Reason,
            Status = c.Status,
            CreatedByName = c.CreatedByUser?.FullName ?? c.CreatedByUser?.Email ?? "Hệ thống",
            CreatedAt = c.CreatedAt,
            VoidedAt = c.VoidedAt
        }).ToList();
    }

    public async Task<RentalChargeDetailResponse> CreateChargeAsync(string orderCode, CreateRentalChargeRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        var charge = new RentalCharge
        {
            RentalChargeId = Guid.NewGuid(),
            RentalOrderId = rentalOrder.OrderId,
            EquipmentAssetId = request.EquipmentAssetId,
            ChargeType = request.ChargeType,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            Status = "Draft",
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.RentalCharges.AddAsync(charge, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.RentalCharges.GetChargeByIdWithDetailsAsync(charge.RentalChargeId, cancellationToken);
        return new RentalChargeDetailResponse
        {
            ChargeId = charge.RentalChargeId,
            OrderCode = orderCode,
            ChargeType = charge.ChargeType,
            EquipmentAssetCode = created?.EquipmentAsset?.AssetCode,
            ProductName = created?.EquipmentAsset?.ProductVariant?.Product?.Name,
            Amount = charge.Amount,
            Reason = charge.Reason,
            Status = charge.Status,
            CreatedByName = created?.CreatedByUser?.FullName ?? "Staff",
            CreatedAt = charge.CreatedAt
        };
    }

    public async Task<bool> AssessChargeAsync(Guid chargeId, CancellationToken cancellationToken = default)
    {
        var charge = await _unitOfWork.RentalCharges.GetChargeByIdWithDetailsAsync(chargeId, cancellationToken);
        if (charge == null)
        {
            throw new NotFoundException($"Không tìm thấy khoản phí với ID '{chargeId}'.");
        }

        if (charge.Status != "Draft")
        {
            throw new ValidationException("chargeId", $"Chỉ khoản phí ở trạng thái 'Draft' mới được chốt sang 'Assessed'. Trạng thái hiện tại: '{charge.Status}'.");
        }

        charge.Status = "Assessed";
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> VoidChargeAsync(Guid chargeId, VoidRentalChargeRequest request, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var charge = await _unitOfWork.RentalCharges.GetChargeByIdWithDetailsAsync(chargeId, cancellationToken);
        if (charge == null)
        {
            throw new NotFoundException($"Không tìm thấy khoản phí với ID '{chargeId}'.");
        }

        charge.Status = "Voided";
        charge.VoidedAt = DateTime.UtcNow;
        charge.Reason = string.IsNullOrWhiteSpace(charge.Reason)
            ? $"[Đã miễn/hủy]: {request.Reason}"
            : $"{charge.Reason} | [Đã miễn/hủy]: {request.Reason}";

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteRentalOrderAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        var rentalOrder = await _unitOfWork.RentalOrders.GetByOrderCodeWithFulfillmentAsync(orderCode, cancellationToken);
        if (rentalOrder == null)
        {
            throw new NotFoundException($"Không tìm thấy đơn thuê với mã '{orderCode}'.");
        }

        // 1. Kiểm tra: không còn thiết bị nào đang Issued
        var activeIssued = rentalOrder.RentalOrderItems
            .SelectMany(i => i.AssetAssignments)
            .Any(a => a.Status == "Issued");

        if (activeIssued)
        {
            throw new ValidationException("orderCode", "Không thể nghiệm thu đóng đơn: Vẫn còn thiết bị chưa được quét thu hồi hoặc báo mất.");
        }

        // 2. Không còn khoản phí nào ở trạng thái Draft
        var draftCharges = rentalOrder.RentalCharges.Any(c => c.Status == "Draft");
        if (draftCharges)
        {
            throw new ValidationException("orderCode", "Không thể nghiệm thu đóng đơn: Vẫn còn biên bản phí ở trạng thái Nháp (Draft). Vui lòng chốt hoặc xóa trước.");
        }

        // 3. Mọi khoản phí Assessed đã được thanh toán hoặc miễn
        var unpaidCharges = rentalOrder.RentalCharges.Any(c => c.Status == "Assessed");
        if (unpaidCharges)
        {
            throw new ValidationException("orderCode", "Không thể nghiệm thu đóng đơn: Vẫn còn khoản phạt chưa được thanh toán hoặc chưa được Admin miễn giảm.");
        }

        rentalOrder.Order.Status = "Completed";
        rentalOrder.Order.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
