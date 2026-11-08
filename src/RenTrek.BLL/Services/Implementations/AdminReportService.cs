using System.Globalization;
using System.Text;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Dashboard, cảnh báo, KPI và các báo cáo vận hành.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminReportService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminReportService
{
    private const int AlertLimit = 20;

    public async Task<AdminDashboardStatsResponse> GetStatsAsync(CancellationToken cancellationToken)
    {
        var x = await _unitOfWork.AdminOperations.GetDashboardStatsAsync(OrderScope, _timeProvider.GetUtcNow(), cancellationToken);
        return new(x.PendingPaymentExpiringCount, x.TripsDepartingIn7DaysCount, x.UnassignedParticipantsCount, x.MissingProvidedItemsCount,
            x.UnderCapacityTripsCount, x.OverdueRentalAssetsCount, x.PendingApprovalRefundsCount, x.FailedRefundsCount,
            x.NeedsReconciliationOrdersCount, x.DraftRentalChargesCount);
    }

    public async Task<List<AdminAlertResponse>> GetAlertsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var reports = _unitOfWork.AdminOperations;
        var alerts = new List<AdminAlertResponse>();
        var missing = await reports.GetMissingGearAsync(OrderScope, now, AlertLimit, cancellationToken);
        alerts.AddRange(missing.Select(x => new AdminAlertResponse("TripMissingGear", "High", $"Chuyến {x.TourName} thiếu đồ cấp", $"Cần chuẩn bị thêm {x.RequiredQuantity - x.AssignedQuantity} thiết bị", "Trip", x.TripId.ToString(), $"/ops/trips/{x.TripId}/provided-items")));
        var overdue = await reports.GetOverdueRentalOrdersAsync(OrderScope, Now, AlertLimit, cancellationToken);
        alerts.AddRange(overdue.Select(x => new AdminAlertResponse("OverdueRental", "Medium", $"Đơn {x.OrderCode} quá hạn trả", "Còn thiết bị chưa được hoàn trả", "RentalOrder", x.OrderId.ToString(), $"/admin/rental-orders/{x.OrderCode}")));
        var expiring = await reports.GetExpiringOrdersAsync(OrderScope, now, now.AddMinutes(15), AlertLimit, cancellationToken);
        alerts.AddRange(expiring.Select(x => new AdminAlertResponse("PaymentExpiring", "Medium", $"Đơn {x.OrderCode} sắp hết hạn", "Kiểm tra thanh toán trước khi hết hạn giữ chỗ", "Order", x.OrderId.ToString(), $"/admin/orders/{x.OrderCode}")));
        if (IsAdmin)
        {
            var failed = await reports.GetFailedRefundOrdersAsync(AlertLimit, cancellationToken);
            alerts.AddRange(failed.Select(x => new AdminAlertResponse("FailedRefund", "High", $"Hoàn tiền đơn {x.OrderCode} thất bại", "Kiểm tra hàng đợi hoàn tiền", "Order", x.OrderId.ToString(), $"/admin/orders/{x.OrderCode}")));
        }
        return alerts;
    }

    public async Task<AdminKpisResponse> GetKpisAsync(CancellationToken cancellationToken)
    {
        RequireAdmin(); var now = _timeProvider.GetUtcNow(); var since = now.AddDays(-7);
        var occupancy = await GetOccupancyAsync(new AdminQueryRequest { From = since.UtcDateTime, To = now.UtcDateTime }, cancellationToken);
        var payments = await _unitOfWork.AdminOperations.GetPaymentTotalsAsync(since, now, cancellationToken);
        var counts = await _unitOfWork.AdminOperations.GetKpiCountsAsync(since, now, cancellationToken);
        return new(counts.BookingCount,
            occupancy.Count == 0 ? 0 : Math.Round(occupancy.Average(x => x.OccupancyRate), 2), payments.Collections - payments.Refunds,
            counts.IssuedAssetCount, counts.AvailableAssetCount);
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
        var rows = await _unitOfWork.AdminOperations.GetSucceededPaymentsAsync(from, to, cancellationToken);
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
        var rows = await _unitOfWork.AdminOperations.GetOccupancyAsync(from, to, request.TourId, cancellationToken);
        return rows.Select(x => new AdminOccupancyResponse(x.TripId, x.TourName, x.StartAt, x.TotalSlots, x.BookedCount,
            x.TotalSlots == 0 ? 0 : Math.Round(100m * x.BookedCount / x.TotalSlots, 2))).OrderBy(x => x.StartAt).ToList();
    }

    public async Task<List<AdminUtilizationResponse>> GetUtilizationAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin(); var (from, to) = ReportPeriod(request);
        var assets = await _unitOfWork.AdminOperations.GetAssetUsageAsync(from, to, request.CategoryId, cancellationToken);
        var days = (decimal)(to - from).TotalDays;
        return assets.Select(x =>
        {
            var intervals = x.Periods.Select(a => (Start: new[] { a.AssignedAt, a.ReservedFrom, from }.Max(), End: new[] { a.ReturnedAt ?? Now, to, Now }.Min()))
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
        var totals = await _unitOfWork.AdminOperations.GetPaymentTotalsAsync(from, to, cancellationToken);
        return new(totals.Refunds, totals.AdditionalCharges);
    }

    /// <summary>Chuyển dữ liệu doanh thu đã tổng hợp thành CSV UTF-8 có BOM, không truy vấn lại DB.</summary>
    public (byte[] FileBytes, string FileName) FormatRevenueCsv(IReadOnlyList<AdminRevenueResponse> items)
    {
        RequireAdmin();
        var sb = new StringBuilder();
        sb.Append("KyBaoCao,LoaiDonHang,TongThuVnd,TongHoanVnd,DoanhThuRongVnd\r\n");

        foreach (var item in items)
        {
            var periodVn = new DateTimeOffset(DateTime.SpecifyKind(item.Period, DateTimeKind.Utc), TimeSpan.Zero)
                .ToOffset(TimeSpan.FromHours(7))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            sb.Append(CsvHelper.BuildRow(
                    periodVn,
                    item.OrderType,
                    item.Collections.ToString("0.##", CultureInfo.InvariantCulture),
                    item.Refunds.ToString("0.##", CultureInfo.InvariantCulture),
                    item.NetRevenueVnd.ToString("0.##", CultureInfo.InvariantCulture)))
                .Append("\r\n");
        }

        var todayVn = DateOnly.FromDateTime(_timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (CsvHelper.ToUtf8WithBom(sb.ToString()), $"revenue-{todayVn}.csv");
    }

    /// <summary>Chuyển dữ liệu hoàn tiền và phụ phí thành CSV UTF-8 có BOM, không truy vấn lại DB.</summary>
    public (byte[] FileBytes, string FileName) FormatRefundChargesCsv(AdminRefundChargeResponse report)
    {
        RequireAdmin();
        var sb = new StringBuilder();
        sb.Append("TongTienHoanVnd,TongPhuPhiThuDuocVnd\r\n");
        sb.Append(CsvHelper.BuildRow(
                report.RefundedAmount.ToString("0.##", CultureInfo.InvariantCulture),
                report.CollectedChargesAmount.ToString("0.##", CultureInfo.InvariantCulture)))
            .Append("\r\n");

        var todayVn = DateOnly.FromDateTime(_timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (CsvHelper.ToUtf8WithBom(sb.ToString()), $"refunds-and-charges-{todayVn}.csv");
    }
}
