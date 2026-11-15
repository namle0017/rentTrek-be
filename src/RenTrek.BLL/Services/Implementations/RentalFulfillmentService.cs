using System.Globalization;
using System.Text;
using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalOps.Requests;
using RenTrek.BLL.DTOs.RentalOps.Responses;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Vận hành đơn thuê (Staff/Admin): giao đồ → nhận trả từng thiết bị → phí trễ / hỏng / mất → đóng đơn.
/// Mọi mốc thời gian lấy từ TimeProvider nên test được bằng đồng hồ giả lập ở Development.
/// Quyết toán cọc chỉ tính số (PRD §8.7); việc chuyển tiền hoàn / thu thêm chưa có luồng tự động.
/// </summary>
/// <remarks>
/// Gộp luồng trùng (2026-11-05, Tri Tin refactor, commit 89892ac): bỏ /admin/management/rental-orders*
/// (AdminRentalOrdersController trong Controllers/Admin + AdminService.Orders, do TIGER.ER viết, commit 6cf600b) và
/// FulfillmentService / RentalHandoverController / RentalChargesController theo orderCode (do vietbach17 viết, commit 88a8961).
/// </remarks>
public sealed class RentalFulfillmentService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IRentalPolicyService? rentalPolicyService = null,
    IRentalSettlementService? settlementService = null,
    IRentalNoticeDispatcher? noticeDispatcher = null,
    IRentalReportService? reportService = null) : IRentalFulfillmentService
{
    private readonly IRentalPolicyService _policies = rentalPolicyService ?? new RentalPolicyService(unitOfWork, timeProvider);

    // ponytail: hằng số; đưa vào system_configurations nếu vận hành cần chỉnh theo từng kho
    private const int HandoverEarlyHours = 24;

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    // ---------- Đọc ----------

    public async Task<PagedResponse<AdminRentalOrderListItemResponse>> GetListAsync(
        AdminRentalOrderQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await unitOfWork.RentalOrders.GetAdminPagedAsync(new AdminRentalOrderFilter
        {
            Status = query.Status,
            OverdueOnly = query.OverdueOnly,
            Search = query.Search,
            Now = timeProvider.GetUtcNow().UtcDateTime,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        }, cancellationToken);

        return new PagedResponse<AdminRentalOrderListItemResponse>
        {
            Items = items.Select(o => new AdminRentalOrderListItemResponse
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                CustomerName = o.CustomerName,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus,
                RentalStartAt = o.RentalStartAt,
                RentalEndAt = o.RentalEndAt,
                TotalAmount = o.TotalAmount,
                AssetsCount = o.AssetsCount,
                IssuedCount = o.IssuedCount,
                ReturnedCount = o.ReturnedCount,
                DraftChargesCount = o.DraftChargesCount,
                IsOverdue = o.IsOverdue
            }).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalItems = total
        };
    }

    public async Task<AdminRentalOrderDetailResponse> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var detail = await unitOfWork.RentalOrders.GetDetailAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);

        var graceHours = await GetGraceHoursAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var response = MapDetail(detail, now, graceHours);

        var policy = await _policies.GetSnapshotAsync(detail.RentalPolicyId, cancellationToken);
        response.RentalPolicy = _policies.ToResponse(policy);
        response.HandedOverAt = detail.HandedOverAt is { } handed ? RentalPeriodCalculator.ToUtc(handed) : null;
        response.CustomerReceivedConfirmedAt = detail.CustomerReceivedConfirmedAt is { } confirmed ? RentalPeriodCalculator.ToUtc(confirmed) : null;
        response.ReceiptConfirmSource = detail.ReceiptConfirmSource;
        response.CancellationType = detail.CancellationType;
        response.EvidencePhotos = (await unitOfWork.RentalEvidencePhotos.GetByOrderAsync(orderId, cancellationToken))
            .Select(p => new RentalEvidencePhotoResponse
            {
                Id = p.RentalEvidencePhotoId,
                AssetAssignmentId = p.AssetAssignmentId,
                Stage = p.Stage,
                ImageUrl = p.ImageUrl,
                Note = p.Note,
                CreatedAt = RentalPeriodCalculator.ToUtc(p.CreatedAt)
            }).ToList();
        if (reportService is not null)
            response.Reports = (await reportService.GetByOrderAsync(orderId, cancellationToken)).ToList();

        var stored = await unitOfWork.RentalSettlements.GetByOrderAsync(orderId, cancellationToken);
        if (stored is not null)
        {
            response.StoredSettlement = new RentalOrderSettlementResponse
            {
                RentalFeePaid = stored.RentalFeePaid,
                DepositHeld = stored.DepositHeld,
                TotalCharges = stored.TotalCharges,
                RentalFeeRefund = stored.RentalFeeRefund,
                DepositRefund = stored.DepositRefund,
                AmountOwed = stored.AmountOwed,
                SettledAt = RentalPeriodCalculator.ToUtc(stored.SettledAt)
            };
        }

        var actions = response.AllowedActions;
        var pickup = RentalPenaltyCalculator.GetPickupStatus(
            policy, detail.DepositAmount, RentalPeriodCalculator.ToUtc(detail.RentalStartAt), RentalPeriodCalculator.ToUtc(detail.RentalEndAt), now);
        var notHandedOver = detail.HandedOverAt is null && !detail.Assignments.Any(a => a.Status is AssignmentStatuses.Issued or AssignmentStatuses.Returned or AssignmentStatuses.LostOrException);
        actions.CanReportNoShow = detail.Status == OrderStatuses.Confirmed && detail.PaymentStatus == OrderPaymentStatuses.Paid && notHandedOver && pickup.IsNoShow;
        actions.MinPhotosPerAsset = policy.MinPhotosPerAsset;
        var hasOpenInspection = response.Reports.Any(r => r.ReportType == RentalReportTypes.ReturnInspection && r.Status == RentalReportStatuses.Submitted);
        var allBack = detail.Status == OrderStatuses.InFulfillment
                      && !detail.Assignments.Any(a => a.Status is AssignmentStatuses.Reserved or AssignmentStatuses.Issued);
        actions.CanSubmitReturnReport = allBack && !hasOpenInspection;
        if (!IsAdmin)
        {
            actions.CanComplete = false;
            actions.CompleteBlockedReason = "Chỉ admin quyết toán; staff gửi báo cáo kiểm tra trả đồ";
        }
        return response;
    }

    /// <summary>Lấy danh sách thiết bị cần giao theo ngày giờ Việt Nam (mặc định hôm nay UTC+7).</summary>
    public async Task<AdminRentalScheduleResponse> GetHandoverListAsync(
        DateOnly? targetDate,
        CancellationToken cancellationToken = default)
    {
        var (date, startUtc, endUtc) = ResolveVietnamDayRange(targetDate);
        var rows = await unitOfWork.RentalOrders.GetHandoverScheduleAsync(startUtc, endUtc, cancellationToken);
        return MapSchedule(date, rows);
    }

    /// <summary>Xuất CSV danh sách thiết bị cần giao theo ngày giờ Việt Nam, kèm UTF-8 BOM.</summary>
    public async Task<(byte[] FileBytes, string FileName)> ExportHandoverListCsvAsync(
        DateOnly? targetDate,
        CancellationToken cancellationToken = default)
    {
        var schedule = await GetHandoverListAsync(targetDate, cancellationToken);
        var csv = BuildScheduleCsv("GioNhan", schedule.Items);
        var fileName = $"handover-{schedule.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";
        return (CsvHelper.ToUtf8WithBom(csv), fileName);
    }

    /// <summary>Lấy danh sách thiết bị cần nhận lại theo ngày giờ Việt Nam (mặc định hôm nay UTC+7).</summary>
    public async Task<AdminRentalScheduleResponse> GetReturnListAsync(
        DateOnly? targetDate,
        CancellationToken cancellationToken = default)
    {
        var (date, startUtc, endUtc) = ResolveVietnamDayRange(targetDate);
        var rows = await unitOfWork.RentalOrders.GetReturnScheduleAsync(startUtc, endUtc, cancellationToken);
        return MapSchedule(date, rows);
    }

    /// <summary>Xuất CSV danh sách thiết bị cần nhận lại theo ngày giờ Việt Nam, kèm UTF-8 BOM.</summary>
    public async Task<(byte[] FileBytes, string FileName)> ExportReturnListCsvAsync(
        DateOnly? targetDate,
        CancellationToken cancellationToken = default)
    {
        var schedule = await GetReturnListAsync(targetDate, cancellationToken);
        var csv = BuildScheduleCsv("GioTra", schedule.Items);
        var fileName = $"return-{schedule.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";
        return (CsvHelper.ToUtf8WithBom(csv), fileName);
    }

    /// <summary>Lịch sử điều chỉnh (gia hạn) của đơn thuê cho màn vận hành, cũ nhất trước.</summary>
    public async Task<List<RentalOrderAdjustmentResponse>> GetAdjustmentsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var rows = await unitOfWork.RentalOrders.GetAdjustmentsAsync(orderId, null, cancellationToken);
        if (rows is null)
        {
            throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);
        }

        return rows.Select(MapAdjustmentResponse).ToList();
    }

    private static RentalOrderAdjustmentResponse MapAdjustmentResponse(RentalOrderAdjustmentRow r) => new()
    {
        Id = r.Id,
        AdjustmentType = r.AdjustmentType,
        OldEndAt = RentalPeriodCalculator.ToUtc(r.OldEndAt),
        NewEndAt = RentalPeriodCalculator.ToUtc(r.NewEndAt),
        ExtraAmount = r.ExtraAmount,
        RequestedByUserId = r.RequestedByUserId,
        RequestedByName = r.RequestedByName,
        CreatedAt = RentalPeriodCalculator.ToUtc(r.CreatedAt)
    };

    private (DateOnly Date, DateTime RangeStartUtc, DateTime RangeEndUtc) ResolveVietnamDayRange(DateOnly? targetDate)
    {
        var date = targetDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(VietnamOffset).DateTime);
        var startLocal = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), VietnamOffset);
        var startUtc = startLocal.UtcDateTime;
        return (date, startUtc, startUtc.AddDays(1));
    }

    private static AdminRentalScheduleResponse MapSchedule(DateOnly date, IReadOnlyList<AdminRentalScheduleRow> rows)
        => new()
        {
            Date = date,
            TotalItems = rows.Count,
            Items = rows.Select(r => new AdminRentalScheduleItemResponse
            {
                OrderId = r.OrderId,
                OrderCode = r.OrderCode,
                CustomerName = r.CustomerName,
                CustomerPhone = r.CustomerPhone,
                ProductName = r.ProductName,
                VariantName = r.VariantName,
                AssetCode = r.AssetCode,
                ScheduledAt = RentalPeriodCalculator.ToUtc(r.ScheduledAt),
                Status = r.Status
            }).ToList()
        };

    private static string BuildScheduleCsv(string timeColumnHeader, IReadOnlyList<AdminRentalScheduleItemResponse> items)
    {
        var sb = new StringBuilder();
        sb.Append("MaDon,TenKhach,SDT,SanPham,BienThe,MaTaiSan,")
            .Append(timeColumnHeader)
            .Append(",TrangThai\r\n");

        foreach (var item in items)
        {
            var scheduledVn = new DateTimeOffset(RentalPeriodCalculator.ToUtc(item.ScheduledAt), TimeSpan.Zero)
                .ToOffset(VietnamOffset)
                .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

            sb.Append(CsvHelper.BuildRow(
                    item.OrderCode,
                    item.CustomerName,
                    item.CustomerPhone,
                    item.ProductName,
                    item.VariantName,
                    item.AssetCode,
                    scheduledVn,
                    item.Status))
                .Append("\r\n");
        }

        return sb.ToString();
    }

    // ---------- Người phụ trách (FR-R05) ----------

    // Staff nhận đơn chưa có ai phụ trách; Admin được nhận lại đơn của người khác. Mọi lần đổi đều ghi audit.
    public Task<AdminRentalOrderDetailResponse> ClaimAsync(Guid orderId, CancellationToken cancellationToken = default)
        => MutateAsync(orderId, "RentalClaim", (order, _, actorId) =>
        {
            EnsureStatus(order, OrderStatuses.PendingPayment, OrderStatuses.Confirmed, OrderStatuses.InFulfillment);
            var rental = order.RentalOrder!;
            var previous = rental.HandledByUserId;
            if (!RentalFulfillmentRules.CanClaim(previous, actorId, IsAdmin))
                throw new ConflictException("Đơn thuê đã có người phụ trách");

            rental.HandledByUserId = actorId;
            return new { previousHandledByUserId = previous, handledByUserId = actorId };
        }, cancellationToken);

    // ---------- Giao đồ (FR-R06) ----------

    public Task<AdminRentalOrderDetailResponse> HandoverAsync(
        Guid orderId,
        HandoverRentalRequest request,
        CancellationToken cancellationToken = default)
        => IssueAsync(orderId, "RentalHandover",
            reserved => RentalFulfillmentRules.PickForHandover(reserved, request.AssignmentIds), cancellationToken);

    public Task<AdminRentalOrderDetailResponse> ScanHandoverAsync(
        Guid orderId,
        ScanHandoverRequest request,
        CancellationToken cancellationToken = default)
        => IssueAsync(orderId, "RentalHandoverScan",
            reserved => RentalFulfillmentRules.PickByCode(reserved, request.AssetCode), cancellationToken);

    private async Task<AdminRentalOrderDetailResponse> IssueAsync(
        Guid orderId,
        string auditAction,
        Func<List<AssetAssignment>, List<AssetAssignment>> pick,
        CancellationToken cancellationToken)
    {
        var photoCounts = await unitOfWork.RentalEvidencePhotos.CountByAssignmentAsync(orderId, RentalEvidenceStages.Handover, cancellationToken);
        var minPhotos = (await PolicyForOrderAsync(orderId, cancellationToken)).MinPhotosPerAsset;
        Order? issuedOrder = null;
        var result = await MutateAsync(orderId, auditAction, (order, now, actorId) =>
        {
            EnsureStatus(order, OrderStatuses.Confirmed, OrderStatuses.InFulfillment);
            if (order.PaymentStatus != OrderPaymentStatuses.Paid)
                throw new ConflictException("Đơn chưa thanh toán, chưa thể giao đồ");

            var rental = order.RentalOrder!;
            var start = RentalPeriodCalculator.ToUtc(rental.RentalStartAt);
            var end = RentalPeriodCalculator.ToUtc(rental.RentalEndAt);
            if (now < start.AddHours(-HandoverEarlyHours))
                throw new ConflictException($"Chỉ giao đồ từ {Format(start.AddHours(-HandoverEarlyHours))} (sớm nhất {HandoverEarlyHours} giờ trước giờ nhận)");
            if (now >= end)
                throw new ConflictException("Đã quá thời gian thuê, không thể giao đồ");

            var reserved = Assignments(order).Where(a => a.Status == AssignmentStatuses.Reserved).ToList();
            if (reserved.Count == 0)
                throw new ConflictException("Không còn thiết bị chờ giao");
            var targets = pick(reserved);

            var unusable = targets
                .Where(a => a.EquipmentAsset.AssetStatus is not (EquipmentAssetStatuses.Available or EquipmentAssetStatuses.Good))
                .Select(a => $"{a.EquipmentAsset.AssetCode} ({a.EquipmentAsset.AssetStatus})")
                .ToList();
            if (unusable.Count > 0)
                throw new ConflictException(
                    $"Thiết bị chưa sẵn sàng để giao: {string.Join(", ", unusable)}. Cần đổi thiết bị khác trước khi giao.",
                    ErrorCodes.ResourceUnavailable);

            // Bằng chứng tình trạng lúc giao: mỗi thiết bị phải có đủ ảnh trước khi bàn giao
            var missingPhotos = targets
                .Where(a => photoCounts.GetValueOrDefault(a.AssetAssignmentId) < minPhotos)
                .Select(a => a.EquipmentAsset.AssetCode)
                .ToList();
            if (missingPhotos.Count > 0)
                throw new ConflictException(
                    $"Cần chụp ít nhất {minPhotos} ảnh cho mỗi thiết bị trước khi giao: {string.Join(", ", missingPhotos)}",
                    ErrorCodes.RentalEvidenceRequired);

            foreach (var assignment in targets)
            {
                assignment.Status = AssignmentStatuses.Issued;
                assignment.AssignedAt = now;
            }

            order.Status = OrderStatuses.InFulfillment;
            rental.HandledByUserId ??= actorId;
            rental.HandedOverAt ??= now;
            issuedOrder = order;

            return new { issued = targets.Select(a => a.EquipmentAsset.AssetCode).ToList() };
        }, cancellationToken);

        if (noticeDispatcher is not null && issuedOrder is not null)
        {
            var queued = await noticeDispatcher.QueueAsync(
                issuedOrder.CustomerUserId,
                issuedOrder.OrderCode,
                issuedOrder.OrderId,
                NotificationTypes.RentalHandedOver,
                "Vui lòng xác nhận đã nhận đồ",
                $"Đơn {issuedOrder.OrderCode} đã được bàn giao. Vui lòng kiểm tra kỹ và bấm \"Đã nhận & kiểm tra\" trong chi tiết đơn.",
                $"RentalHandedOver:{issuedOrder.OrderId}",
                new RentalEmailNotice(
                    "Xác nhận đã nhận đồ",
                    "Thiết bị đã được bàn giao cho bạn",
                    [("Hạn trả", Format(RentalPeriodCalculator.ToUtc(issuedOrder.RentalOrder!.RentalEndAt)))],
                    "Vui lòng kiểm tra kỹ thiết bị và bấm \"Đã nhận & kiểm tra\" trong chi tiết đơn. Nếu có lỗi, hãy báo ngay kèm ảnh. Không phản hồi sau 24 giờ được coi là đã nhận đủ và đúng tình trạng."),
                cancellationToken);
            if (queued)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await noticeDispatcher.FlushAsync(cancellationToken);
            }
        }

        return result;
    }

    private async Task<RentalPolicySnapshot> PolicyForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var rental = await unitOfWork.RentalOrders.GetByIdAsync(orderId, cancellationToken);
        return await _policies.GetSnapshotAsync(rental?.RentalPolicyId, cancellationToken);
    }

    // ---------- Nhận trả (FR-R07) + đề xuất phí trễ ----------

    public async Task<AdminRentalOrderDetailResponse> ReturnAsync(
        Guid orderId,
        ReturnRentalAssetsRequest request,
        CancellationToken cancellationToken = default)
    {
        var graceHours = await GetGraceHoursAsync(cancellationToken);
        var policy = await PolicyForOrderAsync(orderId, cancellationToken);
        var photoCounts = await unitOfWork.RentalEvidencePhotos.CountByAssignmentAsync(orderId, RentalEvidenceStages.Return, cancellationToken);
        var variantValues = await unitOfWork.ProductVariants.GetReplacementValuesAsync(
            await VariantIdsAsync(orderId, cancellationToken), cancellationToken);

        return await MutateAsync(orderId, "RentalReturn", (order, now, actorId) =>
        {
            EnsureStatus(order, OrderStatuses.InFulfillment);

            // Bằng chứng lúc nhận lại: thiết bị trả về (không phải báo mất) phải có đủ ảnh
            var missingPhotos = request.Items
                .Where(i => i.Condition != ReturnConditions.Lost && photoCounts.GetValueOrDefault(i.AssignmentId) < policy.MinPhotosPerAsset)
                .Select(i => i.AssignmentId)
                .ToList();
            if (missingPhotos.Count > 0)
            {
                var codes = Assignments(order).Where(a => missingPhotos.Contains(a.AssetAssignmentId)).Select(a => a.EquipmentAsset.AssetCode);
                throw new ConflictException(
                    $"Cần chụp ít nhất {policy.MinPhotosPerAsset} ảnh tình trạng cho mỗi thiết bị nhận lại: {string.Join(", ", codes)}",
                    ErrorCodes.RentalEvidenceRequired);
            }

            var byId = Assignments(order).ToDictionary(a => a.AssetAssignmentId);
            var returnedAssignments = new List<AssetAssignment>();

            foreach (var item in request.Items)
            {
                if (!byId.TryGetValue(item.AssignmentId, out var assignment))
                    throw new NotFoundException("Thiết bị không thuộc đơn thuê này", ErrorCodes.ItemNotFound);

                // Chặn trả hai lần (retry / bấm đúp) — FR-R07
                if (assignment.Status != AssignmentStatuses.Issued)
                    throw new ConflictException(
                        $"Thiết bị {assignment.EquipmentAsset.AssetCode} không ở trạng thái đang giao (hiện tại: {assignment.Status})");

                var asset = assignment.EquipmentAsset;
                assignment.ReturnCondition = item.Condition;
                if (item.Condition == ReturnConditions.Lost)
                {
                    // Mất: không trở về kho cho thuê (PRD §7.5, BR-22). Đề xuất phí mất = giá trị thiết bị (nháp, admin duyệt)
                    assignment.Status = AssignmentStatuses.LostOrException;
                    asset.AssetStatus = EquipmentAssetStatuses.Lost;
                    var value = RentalOrderOps.AssetReplacementValue(assignment, variantValues);
                    var hasLoss = order.RentalOrder!.RentalCharges.Any(c => c.ChargeType == RentalChargeTypes.Loss
                                                                           && c.EquipmentAssetId == assignment.EquipmentAssetId
                                                                           && c.Status != RentalChargeStatuses.Voided);
                    if (value > 0 && !hasLoss)
                    {
                        order.RentalOrder.RentalCharges.Add(new RentalCharge
                        {
                            RentalChargeId = Guid.NewGuid(),
                            RentalOrderId = order.OrderId,
                            EquipmentAssetId = assignment.EquipmentAssetId,
                            ChargeType = RentalChargeTypes.Loss,
                            Amount = value,
                            Reason = $"Mất thiết bị {asset.AssetCode}: bồi thường giá trị thiết bị",
                            Status = RentalChargeStatuses.Draft,
                            CreatedByUserId = actorId,
                            CreatedAt = now
                        });
                    }
                }
                else
                {
                    assignment.Status = AssignmentStatuses.Returned;
                    assignment.ReturnedAt = now;
                    asset.AssetStatus = item.Condition switch
                    {
                        ReturnConditions.Damaged => EquipmentAssetStatuses.Damaged,
                        ReturnConditions.MissingAccessory => EquipmentAssetStatuses.Maintenance,
                        _ => EquipmentAssetStatuses.Available
                    };
                    returnedAssignments.Add(assignment);
                }

                if (!string.IsNullOrWhiteSpace(item.Note))
                    asset.ConditionNote = item.Note.Trim();
                asset.UpdatedAt = now;
            }

            var rental = order.RentalOrder!;
            var end = RentalPeriodCalculator.ToUtc(rental.RentalEndAt);
            var lateDays = RentalFulfillmentCalculator.CalculateLateDays(end, now, graceHours);
            decimal lateFee = 0;
            if (lateDays > 0)
            {
                foreach (var group in returnedAssignments.GroupBy(a => a.RentalOrderItem!))
                {
                    var lineAssetCount = group.Key.AssetAssignments.Count(a => a.Status != AssignmentStatuses.Cancelled);
                    var units = RentalFulfillmentCalculator.CalculateLateUnits(group.Count(), group.Key.Quantity, lineAssetCount);
                    lateFee += RentalFulfillmentCalculator.CalculateLateFee(lateDays, group.Key.DailyPrice, units);
                }
            }

            if (lateFee > 0)
            {
                // Hệ thống chỉ đề xuất (Draft); Staff xác nhận hoặc hủy
                rental.RentalCharges.Add(new RentalCharge
                {
                    RentalChargeId = Guid.NewGuid(),
                    RentalOrderId = rental.OrderId,
                    ChargeType = RentalChargeTypes.LateReturn,
                    Amount = lateFee,
                    Reason = $"Trả trễ {lateDays} ngày so với hạn {Format(end)}: "
                             + string.Join(", ", returnedAssignments.Select(a => a.EquipmentAsset.AssetCode)),
                    Status = RentalChargeStatuses.Draft,
                    CreatedByUserId = actorId,
                    CreatedAt = now
                });
            }

            // Phạt trả trễ theo bậc % cọc tính tới lúc nhận lại (job có thể chưa kịp nâng bậc)
            var returnStatus = RentalPenaltyCalculator.GetReturnStatus(policy, rental.DepositAmount, end, now);
            if (returnStatus.Tier is { } tier && returnStatus.PenaltyFee > 0)
            {
                RentalOrderOps.UpsertTieredCharge(
                    rental, RentalChargeTypes.LateReturnPenalty, returnStatus.PenaltyFee, tier.MinDaysLate,
                    $"Phạt trả trễ {returnStatus.DaysLate} ngày ({tier.PercentOfDeposit:0.##}% cọc)", actorId, now);
            }

            return new
            {
                returned = request.Items.Select(i => new { i.AssignmentId, i.Condition }).ToList(),
                lateDays,
                suggestedLateFee = lateFee,
                penalty = returnStatus.PenaltyFee
            };
        }, cancellationToken);
    }

    // ---------- Phí phát sinh (FR-C02–C04) ----------

    public async Task<AdminRentalOrderDetailResponse> CreateChargeAsync(
        Guid orderId,
        CreateRentalChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var chargeVariantValues = await unitOfWork.ProductVariants.GetReplacementValuesAsync(
            await VariantIdsAsync(orderId, cancellationToken), cancellationToken);
        return await MutateAsync(orderId, "RentalChargeCreate", (order, now, actorId) =>
        {
            EnsureStatus(order, OrderStatuses.InFulfillment);

            Guid? equipmentAssetId = null;
            if (request.AssignmentId is { } assignmentId)
            {
                var assignment = Assignments(order).FirstOrDefault(a => a.AssetAssignmentId == assignmentId)
                    ?? throw new NotFoundException("Thiết bị không thuộc đơn thuê này", ErrorCodes.ItemNotFound);

                // Chỉ thiết bị đã từng giao cho đơn mới chịu phí (UAT-21)
                if (assignment.Status is not (AssignmentStatuses.Issued or AssignmentStatuses.Returned or AssignmentStatuses.LostOrException))
                    throw new ConflictException($"Thiết bị {assignment.EquipmentAsset.AssetCode} chưa từng được giao cho đơn này");

                equipmentAssetId = assignment.EquipmentAssetId;

                if (request.ChargeType is RentalChargeTypes.Damage or RentalChargeTypes.Loss)
                {
                    var value = RentalOrderOps.AssetReplacementValue(assignment, chargeVariantValues);
                    if (value > 0 && request.Amount > value)
                        throw new ConflictException($"Phí {request.ChargeType} không được vượt giá trị thiết bị ({value:N0}đ)");
                }
            }

            var charge = new RentalCharge
            {
                RentalChargeId = Guid.NewGuid(),
                RentalOrderId = order.OrderId,
                EquipmentAssetId = equipmentAssetId,
                ChargeType = request.ChargeType,
                Amount = request.Amount,
                Reason = request.Reason.Trim(),
                // Staff lập phí ở dạng nháp, admin duyệt khi xem báo cáo kiểm tra trả đồ; admin lập thì chốt luôn
                Status = IsAdmin ? RentalChargeStatuses.Assessed : RentalChargeStatuses.Draft,
                CreatedByUserId = actorId,
                CreatedAt = now
            };
            order.RentalOrder!.RentalCharges.Add(charge);

            return new { charge.RentalChargeId, charge.ChargeType, charge.Amount, charge.Status };
        }, cancellationToken);
    }

    private async Task<List<Guid>> VariantIdsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken);
        return order is null
            ? []
            : Assignments(order).Select(a => a.EquipmentAsset.ProductVariantId).Distinct().ToList();
    }

    public Task<AdminRentalOrderDetailResponse> AssessChargeAsync(
        Guid orderId,
        Guid chargeId,
        CancellationToken cancellationToken = default)
        => MutateAsync(orderId, "RentalChargeAssess", (order, _, _) =>
        {
            var charge = FindCharge(order, chargeId);
            if (charge.Status != RentalChargeStatuses.Draft)
                throw new ConflictException("Chỉ xác nhận được phí đang ở trạng thái nháp", ErrorCodes.ChargeNotReviewable);

            charge.Status = RentalChargeStatuses.Assessed;
            return new { chargeId, charge.Amount };
        }, cancellationToken);

    public Task<AdminRentalOrderDetailResponse> VoidChargeAsync(
        Guid orderId,
        Guid chargeId,
        VoidRentalChargeRequest request,
        CancellationToken cancellationToken = default)
        => MutateAsync(orderId, "RentalChargeVoid", (order, now, _) =>
        {
            var charge = FindCharge(order, chargeId);
            if (charge.Status is not (RentalChargeStatuses.Draft or RentalChargeStatuses.Assessed))
                throw new ConflictException("Phí này không thể hủy", ErrorCodes.ChargeNotReviewable);
            if (!RentalFulfillmentRules.CanVoid(charge.Status, IsAdmin))
                throw new ForbiddenException("Chỉ quản trị viên được hủy phí đã xác nhận");

            // Không xóa: giữ dấu vết, lý do nằm trong audit log (FR-C04)
            var previousStatus = charge.Status;
            charge.Status = RentalChargeStatuses.Voided;
            charge.VoidedAt = now;
            return new { chargeId, previousStatus, charge.Amount, reason = request.Reason.Trim() };
        }, cancellationToken);

    // ---------- Đóng đơn (FR-R08) ----------

    public async Task<AdminRentalOrderDetailResponse> CompleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (settlementService is null)
            return await LegacyCompleteAsync(orderId, cancellationToken);
        if (!IsAdmin)
            throw new ForbiddenException("Chỉ admin quyết toán đơn thuê. Staff vui lòng gửi báo cáo kiểm tra trả đồ.");

        var actorId = currentUser.RequiredUserId;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);
            var now = timeProvider.GetUtcNow();
            var settlement = await settlementService.SettleAsync(order, actorId, now, cancellationToken);
            await unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                ActorUserId = actorId,
                Action = "RentalComplete",
                EntityType = "Order",
                EntityId = orderId,
                Details = JsonSerializer.Serialize(new { settlement.DepositHeld, settlement.TotalCharges, settlement.DepositRefund, settlement.AmountOwed }),
                CreatedAt = now.UtcDateTime
            }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            noticeDispatcher?.Discard();
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (noticeDispatcher is not null)
            await noticeDispatcher.FlushAsync(cancellationToken);
        return await GetDetailAsync(orderId, cancellationToken);
    }

    private Task<AdminRentalOrderDetailResponse> LegacyCompleteAsync(Guid orderId, CancellationToken cancellationToken)
        => MutateAsync(orderId, "RentalComplete", (order, _, _) =>
        {
            EnsureStatus(order, OrderStatuses.InFulfillment);
            if (CompleteBlockedReason(
                    Assignments(order).Select(a => a.Status),
                    order.RentalOrder!.RentalCharges.Select(c => c.Status)) is { } reason)
                throw new ConflictException(reason);

            var settlement = Settle(order.PaymentStatus, order.RentalOrder.DepositAmount, order.RentalOrder.RentalCharges);
            order.Status = OrderStatuses.Completed;
            return new { settlement };
        }, cancellationToken);

    // ---------- Nội bộ ----------

    private async Task<AdminRentalOrderDetailResponse> MutateAsync(
        Guid orderId,
        string auditAction,
        Func<Order, DateTime, Guid, object> apply,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.RequiredUserId;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);

            // PRD §3.2 "G": đơn đã có Staff khác nhận thì chỉ người đó hoặc Admin được thao tác (Claim tự trả Conflict riêng).
            if (auditAction != "RentalClaim"
                && !RentalFulfillmentRules.CanClaim(order.RentalOrder!.HandledByUserId, actorId, IsAdmin))
                throw new ForbiddenException("Đơn thuê đang do nhân viên khác phụ trách");

            var now = timeProvider.GetUtcNow();
            var existingCharges = order.RentalOrder!.RentalCharges.ToHashSet();
            var details = apply(order, now.UtcDateTime, actorId);
            order.UpdatedAt = now;

            // Khóa có HasDefaultValueSql: phí mới gán sẵn Id mà chỉ add qua navigation thì EF tưởng bản ghi cũ (UPDATE 0 dòng).
            foreach (var charge in order.RentalOrder.RentalCharges.Where(c => !existingCharges.Contains(c)))
                await unitOfWork.RentalCharges.AddAsync(charge, cancellationToken);

            await unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                ActorUserId = actorId,
                Action = auditAction,
                EntityType = "Order",
                EntityId = orderId,
                Details = JsonSerializer.Serialize(details),
                CreatedAt = now.UtcDateTime
            }, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetDetailAsync(orderId, cancellationToken);
    }

    private bool IsAdmin => currentUser.Role == RoleNames.Admin;

    private static IEnumerable<AssetAssignment> Assignments(Order order)
        => order.RentalOrder!.RentalOrderItems.SelectMany(i => i.AssetAssignments);

    private static RentalCharge FindCharge(Order order, Guid chargeId)
        => order.RentalOrder!.RentalCharges.FirstOrDefault(c => c.RentalChargeId == chargeId)
           ?? throw new NotFoundException("Không tìm thấy khoản phí", ErrorCodes.ChargeNotFound);

    private static void EnsureStatus(Order order, params string[] allowed)
    {
        if (!allowed.Contains(order.Status))
            throw new ConflictException($"Thao tác không hợp lệ khi đơn đang ở trạng thái {order.Status}");
    }

    private static string? CompleteBlockedReason(IEnumerable<string> assignmentStatuses, IEnumerable<string> chargeStatuses)
    {
        var statuses = assignmentStatuses.ToList();
        if (statuses.Any(s => s == AssignmentStatuses.Reserved))
            return "Còn thiết bị chưa giao";
        if (statuses.Any(s => s == AssignmentStatuses.Issued))
            return "Còn thiết bị chưa nhận trả";
        if (chargeStatuses.Any(s => s == RentalChargeStatuses.Draft))
            return "Còn phí nháp chưa xác nhận hoặc hủy";
        return null;
    }

    // Cọc chỉ tính là đang giữ khi đơn đã thanh toán (luồng hiện tại thu cọc cùng lần thanh toán đơn)
    private static AdminRentalSettlementResponse Settle(string paymentStatus, decimal depositAmount, IEnumerable<RentalCharge> charges)
        => Settle(paymentStatus, depositAmount, charges.Where(c => c.Status == RentalChargeStatuses.Assessed).Sum(c => c.Amount));

    private static AdminRentalSettlementResponse Settle(string paymentStatus, decimal depositAmount, decimal validCharges)
    {
        var depositHeld = paymentStatus == OrderPaymentStatuses.Paid ? depositAmount : 0m;
        var (toRefund, due) = RentalFulfillmentCalculator.Settle(depositHeld, validCharges);
        return new AdminRentalSettlementResponse
        {
            DepositHeld = depositHeld,
            ValidCharges = validCharges,
            DepositToRefund = toRefund,
            AdditionalAmountDue = due
        };
    }

    private async Task<decimal> GetGraceHoursAsync(CancellationToken cancellationToken)
    {
        var raw = await unitOfWork.SystemConfigurations.GetValueAsync(RentalConfigKeys.LateGraceHours, cancellationToken);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours) && hours >= 0
            ? hours
            : RentalConfigKeys.DefaultLateGraceHours;
    }

    private static string Format(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(VietnamOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static AdminRentalOrderDetailResponse MapDetail(RentalOrderDetailRow detail, DateTime now, decimal graceHours)
    {
        var start = RentalPeriodCalculator.ToUtc(detail.RentalStartAt);
        var end = RentalPeriodCalculator.ToUtc(detail.RentalEndAt);

        var assignments = detail.Assignments
            .Where(a => a.RentalOrderItemId.HasValue && a.Status != AssignmentStatuses.Cancelled)
            .ToList();
        var statuses = assignments.Select(a => a.Status).ToList();
        var hasReserved = statuses.Contains(AssignmentStatuses.Reserved);
        var hasIssued = statuses.Contains(AssignmentStatuses.Issued);

        // Trễ hạn: tính như thể nhận trả toàn bộ thiết bị còn ở khách ngay bây giờ
        var isOverdue = hasIssued && now > end;
        var lateDays = isOverdue ? RentalFulfillmentCalculator.CalculateLateDays(end, now, graceHours) : 0;
        decimal suggestedLateFee = 0;
        if (lateDays > 0)
        {
            foreach (var line in detail.Lines)
            {
                var lineAssignments = assignments.Where(a => a.RentalOrderItemId == line.ItemId).ToList();
                var issued = lineAssignments.Count(a => a.Status == AssignmentStatuses.Issued);
                var units = RentalFulfillmentCalculator.CalculateLateUnits(issued, line.Quantity, lineAssignments.Count);
                suggestedLateFee += RentalFulfillmentCalculator.CalculateLateFee(lateDays, line.DailyPrice, units);
            }
        }

        var validCharges = detail.Charges.Where(c => c.Status == RentalChargeStatuses.Assessed).Sum(c => c.Amount);
        var paidAmount = detail.Payments.Where(p => p.PaymentType == "Collection" && p.Status == PaymentStatuses.Succeeded).Sum(p => p.Amount);
        var refundedAmount = detail.Payments.Where(p => p.PaymentType == "Refund" && p.Status == PaymentStatuses.Succeeded).Sum(p => p.Amount);

        var isActive = detail.Status is OrderStatuses.Confirmed or OrderStatuses.InFulfillment;
        var handoverOpensAt = start.AddHours(-HandoverEarlyHours);
        string? handoverBlocked = null;
        if (!isActive)
            handoverBlocked = "Đơn không ở trạng thái giao đồ được";
        else if (detail.PaymentStatus != OrderPaymentStatuses.Paid)
            handoverBlocked = "Đơn chưa thanh toán";
        else if (!hasReserved)
            handoverBlocked = "Không còn thiết bị chờ giao";
        else if (now < handoverOpensAt)
            handoverBlocked = $"Chỉ giao đồ từ {Format(handoverOpensAt)}";
        else if (now >= end)
            handoverBlocked = "Đã quá thời gian thuê";

        var inFulfillment = detail.Status == OrderStatuses.InFulfillment;
        var completeBlocked = inFulfillment
            ? CompleteBlockedReason(statuses, detail.Charges.Select(c => c.Status))
            : "Đơn không ở trạng thái đang thực hiện";

        return new AdminRentalOrderDetailResponse
        {
            OrderId = detail.OrderId,
            OrderCode = detail.OrderCode,
            Status = detail.Status,
            PaymentStatus = detail.PaymentStatus,
            CreatedAt = detail.CreatedAt.UtcDateTime,
            RentalStartAt = start,
            RentalEndAt = end,
            Note = detail.Note,
            HandledByName = detail.HandledByName,
            ServerNow = now,
            Customer = new AdminRentalCustomerResponse
            {
                FullName = detail.CustomerName,
                Email = detail.CustomerEmail,
                Phone = detail.CustomerPhone
            },
            Lines = detail.Lines.Select(line => new AdminRentalLineResponse
            {
                RentalOrderItemId = line.ItemId,
                DisplayName = line.BundleId.HasValue
                    ? $"Combo {line.BundleName}"
                    : $"{line.ProductName} — {line.VariantName}",
                IsBundle = line.BundleId.HasValue,
                Quantity = line.Quantity,
                DailyPrice = line.DailyPrice,
                BillableDays = line.BillableDays,
                LineAmount = line.LineAmount
            }).ToList(),
            Assignments = assignments.Select(a => new AdminRentalAssignmentResponse
            {
                AssignmentId = a.AssignmentId,
                RentalOrderItemId = a.RentalOrderItemId!.Value,
                AssetCode = a.AssetCode,
                DisplayName = $"{a.ProductName} — {a.VariantName}",
                Status = a.Status,
                AssetStatus = a.AssetStatus,
                IssuedAt = a.Status == AssignmentStatuses.Reserved ? null : RentalPeriodCalculator.ToUtc(a.AssignedAt),
                ReturnedAt = a.ReturnedAt is null ? null : RentalPeriodCalculator.ToUtc(a.ReturnedAt.Value),
                ReturnCondition = a.ReturnCondition
            }).ToList(),
            Charges = detail.Charges.Select(c => new AdminRentalChargeResponse
            {
                ChargeId = c.Id,
                ChargeType = c.ChargeType,
                Amount = c.Amount,
                Reason = c.Reason,
                AssetCode = c.AssetCode,
                Status = c.Status,
                CreatedAt = RentalPeriodCalculator.ToUtc(c.CreatedAt),
                VoidedAt = c.VoidedAt is null ? null : RentalPeriodCalculator.ToUtc(c.VoidedAt.Value)
            }).ToList(),
            Amounts = new AdminRentalAmountsResponse
            {
                TotalAmount = detail.TotalAmount,
                DepositAmount = detail.DepositAmount,
                PaidAmount = paidAmount,
                RefundedAmount = refundedAmount
            },
            Overdue = new AdminRentalOverdueResponse
            {
                IsOverdue = isOverdue,
                OverdueHours = isOverdue ? (int)Math.Floor((now - end).TotalHours) : 0,
                LateDays = lateDays,
                GraceHours = graceHours,
                SuggestedLateFee = suggestedLateFee
            },
            Settlement = Settle(detail.PaymentStatus, detail.DepositAmount, validCharges),
            AllowedActions = new AdminRentalAllowedActionsResponse
            {
                CanHandover = handoverBlocked is null,
                HandoverBlockedReason = handoverBlocked,
                CanReturn = inFulfillment && hasIssued,
                CanAddCharge = inFulfillment,
                CanComplete = completeBlocked is null,
                CompleteBlockedReason = completeBlocked
            }
        };
    }
}
