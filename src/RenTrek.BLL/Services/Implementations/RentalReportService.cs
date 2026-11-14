using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.RentalReport.Requests;
using RenTrek.BLL.DTOs.RentalReport.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Internal;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <inheritdoc cref="IRentalReportService"/>
public sealed class RentalReportService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IRentalSettlementService settlementService,
    IRentalNoticeDispatcher noticeDispatcher,
    INotificationService notificationService,
    IRentalEvidenceService evidenceService) : IRentalReportService
{
    private const int MaxPageSize = 100;

    public async Task<RentalReportResponse> SubmitReturnInspectionAsync(
        Guid orderId,
        SubmitReturnInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Note?.Trim().Length > 2000)
            throw new BadRequestException("Ghi chú tối đa 2000 ký tự", ErrorCodes.ValidationError);

        var actorId = currentUser.RequiredUserId;
        Guid reportId;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);
            if (order.Status != OrderStatuses.InFulfillment)
                throw new ConflictException("Chỉ gửi báo cáo khi đơn đang thuê và đã nhận lại đồ");
            var statuses = RentalOrderOps.Assignments(order).Select(a => a.Status).ToList();
            if (statuses.Contains(AssignmentStatuses.Reserved) || statuses.Contains(AssignmentStatuses.Issued))
                throw new ConflictException("Còn thiết bị chưa giao hoặc chưa nhận trả");
            if (await unitOfWork.RentalReports.GetOpenAsync(orderId, RentalReportTypes.ReturnInspection, cancellationToken) is not null)
                throw new ConflictException("Đơn đã có báo cáo kiểm tra đang chờ admin duyệt");

            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            var charges = order.RentalOrder!.RentalCharges
                .Where(c => c.Status is RentalChargeStatuses.Draft or RentalChargeStatuses.Assessed)
                .ToList();
            var report = new RentalReport
            {
                RentalReportId = Guid.NewGuid(),
                RentalOrderId = orderId,
                ReportType = RentalReportTypes.ReturnInspection,
                Status = RentalReportStatuses.Submitted,
                Amount = charges.Sum(c => c.Amount),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedByUserId = actorId,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            await unitOfWork.RentalReports.AddAsync(report, cancellationToken);
            reportId = report.RentalReportId;

            foreach (var adminId in await unitOfWork.RefundCases.GetActiveUserIdsByRolesAsync([RoleNames.Admin], cancellationToken))
            {
                await notificationService.QueueAsync(
                    adminId,
                    NotificationTypes.RentalReportSubmitted,
                    "Báo cáo trả đồ chờ duyệt",
                    $"Đơn {order.OrderCode}: {charges.Count} khoản phí, tổng {report.Amount:N0}đ chờ duyệt và quyết toán cọc.",
                    "RentalReport",
                    report.RentalReportId,
                    $"RentalReportSubmitted:{report.RentalReportId}",
                    cancellationToken);
            }

            await AuditAsync(actorId, "RentalReturnInspectionSubmitted", orderId, new { reportId, report.Amount }, nowUtc, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetAsync(reportId, cancellationToken);
    }

    public async Task<PagedResponse<RentalReportResponse>> SearchAsync(RentalReportQueryRequest query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var status = RentalReportStatuses.All.Contains(query.Status ?? string.Empty) ? query.Status : null;
        var type = RentalReportTypes.All.Contains(query.ReportType ?? string.Empty) ? query.ReportType : null;
        var (items, total) = await unitOfWork.RentalReports.SearchAsync(status, type, pageNumber, pageSize, cancellationToken);
        return new PagedResponse<RentalReportResponse>
        {
            Items = items.Select(Map).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    public async Task<RentalReportResponse> GetAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await unitOfWork.RentalReports.GetDetailAsync(reportId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy báo cáo", ErrorCodes.RentalReportNotFound);
        var response = Map(report);
        response.EvidencePhotos = (await evidenceService.GetByOrderAsync(report.RentalOrderId, cancellationToken)).ToList();
        return response;
    }

    public async Task<IReadOnlyList<RentalReportResponse>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var reports = await unitOfWork.RentalReports.GetByOrderAsync(orderId, cancellationToken);
        return reports.Select(r => new RentalReportResponse
        {
            RentalReportId = r.RentalReportId,
            OrderId = r.RentalOrderId,
            ReportType = r.ReportType,
            Status = r.Status,
            Amount = r.Amount,
            Note = r.Note,
            ReviewNote = r.ReviewNote,
            CreatedAt = RentalPeriodCalculator.ToUtc(r.CreatedAt),
            ReviewedAt = r.ReviewedAt is { } at ? RentalPeriodCalculator.ToUtc(at) : null,
            RowVersion = r.RowVersion
        }).ToList();
    }

    public Task<RentalReportResponse> ApproveAsync(Guid reportId, ReviewRentalReportRequest request, CancellationToken cancellationToken = default)
        => ReviewAsync(reportId, request, approve: true, cancellationToken);

    public Task<RentalReportResponse> RejectAsync(Guid reportId, ReviewRentalReportRequest request, CancellationToken cancellationToken = default)
        => ReviewAsync(reportId, request, approve: false, cancellationToken);

    private async Task<RentalReportResponse> ReviewAsync(
        Guid reportId,
        ReviewRentalReportRequest request,
        bool approve,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReviewNote?.Trim().Length > 1000)
            throw new BadRequestException("Ghi chú duyệt tối đa 1000 ký tự", ErrorCodes.ValidationError);

        var adminId = currentUser.RequiredUserId;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var probe = await unitOfWork.RentalReports.GetByIdAsync(reportId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy báo cáo", ErrorCodes.RentalReportNotFound);
            var orderId = probe.RentalOrderId;
            await unitOfWork.Payments.AcquireOrderLockAsync(orderId, cancellationToken);
            await unitOfWork.RentalOrders.AcquireFulfillmentLockAsync(orderId, cancellationToken);
            var report = await unitOfWork.RentalReports.GetDetailAsync(reportId, cancellationToken)!;
            var order = await unitOfWork.RentalOrders.GetForFulfillmentAsync(orderId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy đơn thuê", ErrorCodes.OrderNotFound);

            if (report!.Status != RentalReportStatuses.Submitted)
                throw new ConflictException("Báo cáo đã được xử lý", ErrorCodes.RentalReportNotReviewable);
            if (report.RowVersion != request.ExpectedRowVersion)
                throw new ConflictException("Báo cáo vừa được cập nhật, vui lòng tải lại", ErrorCodes.ConcurrencyConflict);

            var now = timeProvider.GetUtcNow();
            var nowUtc = now.UtcDateTime;
            if (approve)
            {
                switch (report.ReportType)
                {
                    case RentalReportTypes.ReturnInspection:
                        AssessDraftCharges(order);
                        await settlementService.SettleAsync(order, adminId, now, cancellationToken);
                        report.Status = RentalReportStatuses.Approved;
                        break;
                    case RentalReportTypes.SuspectedLoss:
                        await MarkIssuedAsLostAsync(order, adminId, nowUtc, cancellationToken);
                        AssessDraftCharges(order);
                        await settlementService.SettleAsync(order, adminId, now, cancellationToken);
                        report.Status = RentalReportStatuses.Approved;
                        break;
                    case RentalReportTypes.ExtraPayment:
                        ResolveExtraPaymentManually(order, nowUtc);
                        report.Status = RentalReportStatuses.Resolved;
                        break;
                    default:
                        report.Status = RentalReportStatuses.Approved;
                        break;
                }
            }
            else
            {
                if (report.ReportType is not (RentalReportTypes.ReturnInspection or RentalReportTypes.SuspectedLoss))
                    throw new ConflictException("Loại báo cáo này chỉ xác nhận đã xem, không từ chối được", ErrorCodes.RentalReportNotReviewable);
                if (string.IsNullOrWhiteSpace(request.ReviewNote))
                    throw new BadRequestException("Cần ghi lý do từ chối để staff chỉnh lại", ErrorCodes.ValidationError);
                report.Status = RentalReportStatuses.Rejected;
            }

            report.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();
            report.ReviewedByUserId = adminId;
            report.ReviewedAt = nowUtc;
            report.UpdatedAt = nowUtc;
            report.RowVersion++;

            await AuditAsync(adminId, approve ? "RentalReportApproved" : "RentalReportRejected", orderId,
                new { reportId, report.ReportType, report.Status }, nowUtc, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            noticeDispatcher.Discard();
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        await noticeDispatcher.FlushAsync(cancellationToken);
        return await GetAsync(reportId, cancellationToken);
    }

    private static void AssessDraftCharges(Order order)
    {
        foreach (var charge in order.RentalOrder!.RentalCharges.Where(c => c.Status == RentalChargeStatuses.Draft))
            charge.Status = RentalChargeStatuses.Assessed;
    }

    /// <summary>Xác nhận mất: thiết bị còn ở khách chuyển Lost, lập phí mất = giá trị thiết bị (nếu chưa có phí mất cho thiết bị đó).</summary>
    private async Task MarkIssuedAsLostAsync(Order order, Guid adminId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rental = order.RentalOrder!;
        var issued = RentalOrderOps.Assignments(order).Where(a => a.Status == AssignmentStatuses.Issued).ToList();
        var variantValues = await unitOfWork.ProductVariants.GetReplacementValuesAsync(
            issued.Select(a => a.EquipmentAsset.ProductVariantId).Distinct().ToList(), cancellationToken);

        var missingValue = new List<string>();
        foreach (var assignment in issued)
        {
            var hasLossCharge = rental.RentalCharges.Any(c => c.ChargeType == RentalChargeTypes.Loss
                                                           && c.EquipmentAssetId == assignment.EquipmentAssetId
                                                           && c.Status != RentalChargeStatuses.Voided);
            var value = RentalOrderOps.AssetReplacementValue(assignment, variantValues);
            if (!hasLossCharge && value <= 0)
            {
                missingValue.Add(assignment.EquipmentAsset.AssetCode);
                continue;
            }

            assignment.Status = AssignmentStatuses.LostOrException;
            assignment.ReturnCondition = ReturnConditions.Lost;
            assignment.EquipmentAsset.AssetStatus = EquipmentAssetStatuses.Lost;
            assignment.EquipmentAsset.UpdatedAt = nowUtc;
            if (!hasLossCharge)
            {
                var charge = new RentalCharge
                {
                    RentalChargeId = Guid.NewGuid(),
                    RentalOrderId = rental.OrderId,
                    EquipmentAssetId = assignment.EquipmentAssetId,
                    ChargeType = RentalChargeTypes.Loss,
                    Amount = value,
                    Reason = $"Không trả thiết bị {assignment.EquipmentAsset.AssetCode} quá hạn: bồi thường giá trị thiết bị",
                    Status = RentalChargeStatuses.Assessed,
                    CreatedByUserId = adminId,
                    CreatedAt = nowUtc
                };
                rental.RentalCharges.Add(charge);
                await unitOfWork.RentalCharges.AddAsync(charge, cancellationToken);
            }
        }

        if (missingValue.Count > 0)
        {
            throw new ConflictException(
                $"Thiết bị chưa có giá trị đền bù: {string.Join(", ", missingValue)}. Vui lòng lập phí mất thủ công cho các thiết bị này trước khi duyệt.");
        }
    }

    /// <summary>Admin xác nhận đã thu khoản vượt cọc ngoài hệ thống (tiền mặt / chuyển khoản không qua QR).</summary>
    private static void ResolveExtraPaymentManually(Order order, DateTime nowUtc)
    {
        if (order.Status != OrderStatuses.AwaitingSettlement)
            throw new ConflictException("Đơn không ở trạng thái chờ thu thêm", ErrorCodes.RentalReportNotReviewable);

        foreach (var payment in order.Payments.Where(p => p.Purpose == PaymentPurposes.AdditionalCharge && p.Status == PaymentStatuses.Pending))
        {
            payment.Status = PaymentStatuses.Succeeded;
            payment.PaymentMethod = "Manual";
            payment.ProcessedAt = new DateTimeOffset(nowUtc, TimeSpan.Zero);
        }

        order.Status = OrderStatuses.Completed;
        order.UpdatedAt = new DateTimeOffset(nowUtc, TimeSpan.Zero);
    }

    private static RentalReportResponse Map(RentalReport report) => new()
    {
        RentalReportId = report.RentalReportId,
        OrderId = report.RentalOrderId,
        OrderCode = report.RentalOrder?.Order?.OrderCode ?? string.Empty,
        CustomerName = report.RentalOrder?.Order?.CustomerUser?.FullName ?? string.Empty,
        CustomerEmail = report.RentalOrder?.Order?.CustomerUser?.Email,
        OrderStatus = report.RentalOrder?.Order?.Status ?? string.Empty,
        ReportType = report.ReportType,
        Status = report.Status,
        Amount = report.Amount,
        Note = report.Note,
        ReviewNote = report.ReviewNote,
        CreatedByName = report.CreatedByUser?.FullName,
        ReviewedByName = report.ReviewedByUser?.FullName,
        CreatedAt = RentalPeriodCalculator.ToUtc(report.CreatedAt),
        ReviewedAt = report.ReviewedAt is { } at ? RentalPeriodCalculator.ToUtc(at) : null,
        RowVersion = report.RowVersion
    };

    private Task AuditAsync(Guid? actorUserId, string action, Guid orderId, object details, DateTime nowUtc, CancellationToken cancellationToken)
        => unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            EntityType = "Order",
            EntityId = orderId,
            Details = JsonSerializer.Serialize(details),
            CreatedAt = nowUtc
        }, cancellationToken);
}
