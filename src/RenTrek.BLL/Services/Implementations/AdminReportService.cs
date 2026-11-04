using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Dashboard, cảnh báo, KPI và các báo cáo vận hành.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminReportService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminReportService
{
    public async Task<AdminDashboardStatsResponse> GetStatsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow(); var soon = now.AddDays(7); var expiry = now.AddMinutes(15);
        var trips = _unitOfWork.Trips.Query().Where(x => !x.IsDeleted && (IsAdmin || x.ManagedByUserId == ActorId) && x.StartAt >= now && x.StartAt <= soon && x.Status != TripStatuses.Cancelled);
        var tripIds = trips.Select(x => x.TripId); var orders = ScopedOrders(); var orderIds = orders.Select(x => x.OrderId);
        var gear = _unitOfWork.TripProvidedItems.Query().Where(x => tripIds.Contains(x.TripId) && x.RequiredQuantity > x.AssetAssignments.Count(a => a.Status == AssignmentStatuses.Reserved || a.Status == AssignmentStatuses.Issued));
        return new(
            await orders.CountAsync(x => x.Status == OrderStatuses.PendingPayment && x.HoldExpiresAt <= expiry && x.HoldExpiresAt > now, cancellationToken),
            await trips.CountAsync(cancellationToken),
            await _unitOfWork.BookingParticipants.Query().CountAsync(x => !x.IsDeleted && x.Status != BookingParticipantStatuses.Cancelled && x.TripGroupId == null && tripIds.Contains(x.TripId), cancellationToken),
            await gear.CountAsync(cancellationToken),
            await trips.CountAsync(x => x.TripBookingOrders.Where(b => b.Order.Status == OrderStatuses.Confirmed || b.Order.Status == OrderStatuses.InFulfillment).Sum(b => b.ParticipantCount) < x.MinParticipants, cancellationToken),
            await _unitOfWork.AssetAssignments.Query().CountAsync(x => x.Status == AssignmentStatuses.Issued && x.RentalOrderItem != null && x.RentalOrderItem.RentalOrder.RentalEndAt < Now && orderIds.Contains(x.RentalOrderItem.RentalOrderId), cancellationToken),
            IsAdmin ? await _unitOfWork.Payments.Query().CountAsync(x => x.PaymentType == "Refund" && x.Status == PaymentStatuses.PendingApproval, cancellationToken) : 0,
            IsAdmin ? await _unitOfWork.Payments.Query().CountAsync(x => x.PaymentType == "Refund" && x.Status == PaymentStatuses.Failed, cancellationToken) : 0,
            await orders.CountAsync(x => x.PaymentStatus == OrderPaymentStatuses.NeedsReconciliation, cancellationToken),
            await _unitOfWork.RentalCharges.Query().CountAsync(x => x.Status == RentalChargeStatuses.Draft && orderIds.Contains(x.RentalOrderId), cancellationToken));
    }

    public async Task<List<AdminAlertResponse>> GetAlertsAsync(CancellationToken cancellationToken)
    {
        var soon = _timeProvider.GetUtcNow().AddDays(7); var now = _timeProvider.GetUtcNow();
        var alerts = new List<AdminAlertResponse>();
        var missing = await _unitOfWork.TripProvidedItems.Query().Where(x => !x.Trip.IsDeleted && (IsAdmin || x.Trip.ManagedByUserId == ActorId)
            && x.Trip.Status != TripStatuses.Cancelled && x.Trip.StartAt >= now && x.Trip.StartAt <= soon && x.RequiredQuantity > x.AssetAssignments.Count(a => a.Status == AssignmentStatuses.Reserved || a.Status == AssignmentStatuses.Issued))
            .OrderBy(x => x.Trip.StartAt).Take(20).Select(x => new { x.TripId, x.Trip.Tour.Name, x.RequiredQuantity, Assigned = x.AssetAssignments.Count(a => a.Status == AssignmentStatuses.Reserved || a.Status == AssignmentStatuses.Issued) }).ToListAsync(cancellationToken);
        alerts.AddRange(missing.Select(x => new AdminAlertResponse("TripMissingGear", "High", $"Chuyến {x.Name} thiếu đồ cấp", $"Cần chuẩn bị thêm {x.RequiredQuantity - x.Assigned} thiết bị", "Trip", x.TripId.ToString(), $"/ops/trips/{x.TripId}/provided-items")));
        var overdue = await ScopedOrders().Where(x => x.RentalOrder != null && x.RentalOrder.RentalEndAt < Now && x.RentalOrder.RentalOrderItems.Any(i => i.AssetAssignments.Any(a => a.Status == AssignmentStatuses.Issued)))
            .OrderBy(x => x.RentalOrder!.RentalEndAt).Take(20).Select(x => new { x.OrderId, x.OrderCode }).ToListAsync(cancellationToken);
        alerts.AddRange(overdue.Select(x => new AdminAlertResponse("OverdueRental", "Medium", $"Đơn {x.OrderCode} quá hạn trả", "Còn thiết bị chưa được hoàn trả", "RentalOrder", x.OrderId.ToString(), $"/admin/rental-orders/{x.OrderCode}")));
        var expiring = await ScopedOrders().Where(x => x.Status == OrderStatuses.PendingPayment && x.HoldExpiresAt > now && x.HoldExpiresAt <= now.AddMinutes(15))
            .OrderBy(x => x.HoldExpiresAt).Take(20).Select(x => new { x.OrderId, x.OrderCode }).ToListAsync(cancellationToken);
        alerts.AddRange(expiring.Select(x => new AdminAlertResponse("PaymentExpiring", "Medium", $"Đơn {x.OrderCode} sắp hết hạn", "Kiểm tra thanh toán trước khi hết hạn giữ chỗ", "Order", x.OrderId.ToString(), $"/admin/orders/{x.OrderCode}")));
        if (IsAdmin)
        {
            var failed = await _unitOfWork.Payments.Query().Where(x => x.PaymentType == "Refund" && x.Status == PaymentStatuses.Failed).OrderByDescending(x => x.CreatedAt).Take(20)
                .Select(x => new { x.OrderId, x.Order.OrderCode }).ToListAsync(cancellationToken);
            alerts.AddRange(failed.Select(x => new AdminAlertResponse("FailedRefund", "High", $"Hoàn tiền đơn {x.OrderCode} thất bại", "Kiểm tra hàng đợi hoàn tiền", "Order", x.OrderId.ToString(), $"/admin/orders/{x.OrderCode}")));
        }
        return alerts;
    }

    public async Task<AdminKpisResponse> GetKpisAsync(CancellationToken cancellationToken)
    {
        RequireAdmin(); var now = _timeProvider.GetUtcNow(); var since = now.AddDays(-7);
        var occupancy = await GetOccupancyAsync(new AdminQueryRequest { From = since.UtcDateTime, To = now.UtcDateTime }, cancellationToken);
        var payments = _unitOfWork.Payments.Query().Where(x => x.Status == PaymentStatuses.Succeeded && x.ProcessedAt >= since && x.ProcessedAt <= now);
        var collections = await payments.Where(x => x.PaymentType == "Collection").SumAsync(x => x.Amount, cancellationToken);
        var refunds = await payments.Where(x => x.PaymentType == "Refund").SumAsync(x => x.Amount, cancellationToken);
        return new(await _unitOfWork.TripBookingOrders.Query().CountAsync(x => !x.Order.IsDeleted && x.Order.CreatedAt >= since && x.Order.CreatedAt <= now, cancellationToken),
            occupancy.Count == 0 ? 0 : Math.Round(occupancy.Average(x => x.OccupancyRate), 2), collections - refunds,
            await _unitOfWork.AssetAssignments.Query().Where(x => x.Status == AssignmentStatuses.Issued).Select(x => x.EquipmentAssetId).Distinct().CountAsync(cancellationToken),
            await _unitOfWork.EquipmentAssets.Query().CountAsync(x => !x.IsDeleted && x.AssetStatus == EquipmentAssetStatuses.Available, cancellationToken));
    }

    private (DateTime From, DateTime To) ReportPeriod(AdminQueryRequest request)
    {
        var from = request.From ?? Now.AddDays(-30); var to = request.To ?? Now;
        Check(from.Kind == DateTimeKind.Utc && to.Kind == DateTimeKind.Utc && to > from && (to - from).TotalDays <= 731,
            "from", "Khoảng báo cáo phải là giờ UTC, tối đa 2 năm");
        return (from, to);
    }

    public async Task<List<AdminRevenueResponse>> GetRevenueAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var (from, to) = ReportPeriod(request);
        var rows = await _unitOfWork.Payments.Query().Where(x => x.Status == PaymentStatuses.Succeeded && x.ProcessedAt >= from && x.ProcessedAt <= to)
            .Select(x => new { x.Order.OrderType, x.PaymentType, x.Amount, x.ProcessedAt }).ToListAsync(cancellationToken);
        DateTime Period(DateTimeOffset date)
        {
            var day = date.ToOffset(TimeSpan.FromHours(7)).Date;
            var localPeriod = request.GroupBy == "week" ? day.AddDays(-((int)day.DayOfWeek + 6) % 7) : new(day.Year, day.Month, 1);
            return DateTime.SpecifyKind(localPeriod.AddHours(-7), DateTimeKind.Utc);
        }
        return rows.GroupBy(x => new { Period = Period(x.ProcessedAt!.Value), x.OrderType }).Select(g =>
        {
            var collections = g.Where(x => x.PaymentType == "Collection").Sum(x => x.Amount);
            var refunds = g.Where(x => x.PaymentType == "Refund").Sum(x => x.Amount);
            return new AdminRevenueResponse(g.Key.Period, g.Key.OrderType, collections, refunds, collections - refunds);
        }).OrderBy(x => x.Period).ThenBy(x => x.OrderType).ToList();
    }

    public async Task<List<AdminOccupancyResponse>> GetOccupancyAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var (from, to) = ReportPeriod(request);
        var query = _unitOfWork.Trips.Query().Where(x => !x.IsDeleted && x.Status != TripStatuses.Cancelled && x.StartAt >= from && x.StartAt <= to);
        if (request.TourId.HasValue) query = query.Where(x => x.TourId == request.TourId);
        var rows = await query.Select(x => new
        {
            x.TripId,
            x.Tour.Name,
            x.StartAt,
            x.TotalSlots,
            Booked = x.TripBookingOrders.Where(b => b.Order.Status == OrderStatuses.Confirmed || b.Order.Status == OrderStatuses.InFulfillment || b.Order.Status == OrderStatuses.Completed).Sum(b => b.ParticipantCount)
        }).ToListAsync(cancellationToken);
        return rows.Select(x => new AdminOccupancyResponse(x.TripId, x.Name, x.StartAt, x.TotalSlots, x.Booked,
            x.TotalSlots == 0 ? 0 : Math.Round(100m * x.Booked / x.TotalSlots, 2))).OrderBy(x => x.StartAt).ToList();
    }

    public async Task<List<AdminUtilizationResponse>> GetUtilizationAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var (from, to) = ReportPeriod(request);
        var query = _unitOfWork.EquipmentAssets.Query().Where(x => !x.IsDeleted);
        if (request.CategoryId.HasValue) query = query.Where(x => x.ProductVariant.Product.CategoryId == request.CategoryId);
        var assets = await query.Select(x => new
        {
            x.EquipmentAssetId,
            x.AssetCode,
            Assignments = x.AssetAssignments
            .Where(a => (a.Status == AssignmentStatuses.Issued || a.Status == AssignmentStatuses.Returned || a.Status == AssignmentStatuses.LostOrException) && a.AssignedAt < to && (a.ReturnedAt == null || a.ReturnedAt > from))
            .Select(a => new { a.AssignedAt, a.ReservedFrom, a.ReturnedAt }).ToList()
        }).ToListAsync(cancellationToken);
        var days = (decimal)(to - from).TotalDays;
        return assets.Select(x =>
        {
            var intervals = x.Assignments.Select(a => (Start: new[] { a.AssignedAt, a.ReservedFrom, from }.Max(), End: new[] { a.ReturnedAt ?? Now, to, Now }.Min()))
                .Where(a => a.End > a.Start).OrderBy(a => a.Start).ToList();
            var total = TimeSpan.Zero; DateTime? previousEnd = null;
            foreach (var interval in intervals)
            {
                var start = previousEnd.HasValue && previousEnd > interval.Start ? previousEnd.Value : interval.Start;
                if (interval.End > start) total += interval.End - start;
                if (previousEnd == null || interval.End > previousEnd) previousEnd = interval.End;
            }
            var issued = (decimal)total.TotalDays;
            return new AdminUtilizationResponse(x.EquipmentAssetId, x.AssetCode, Math.Round(issued, 2), Math.Round(days, 2), Math.Round(100 * issued / days, 2));
        }).ToList();
    }

    public async Task<AdminRefundChargeResponse> GetRefundChargesAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var (from, to) = ReportPeriod(request);
        var payments = _unitOfWork.Payments.Query().Where(x => x.Status == PaymentStatuses.Succeeded && x.ProcessedAt >= from && x.ProcessedAt <= to);
        return new(await payments.Where(x => x.PaymentType == "Refund").SumAsync(x => x.Amount, cancellationToken),
            await payments.Where(x => x.PaymentType == "Collection" && x.Purpose == "AdditionalCharge").SumAsync(x => x.Amount, cancellationToken));
    }
}
