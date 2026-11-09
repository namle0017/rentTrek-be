using System.Text;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class RentalExportTests
{
    private static readonly DateTimeOffset FixedUtc = new(2026, 11, 8, 18, 0, 0, TimeSpan.Zero); // 2026-11-09 01:00 UTC+7

    [Fact]
    public void CsvHelper_EscapesCommasQuotesNewlinesAndIncludesUtf8Bom()
    {
        Assert.Equal(string.Empty, CsvHelper.EscapeCsv(null));
        Assert.Equal(string.Empty, CsvHelper.EscapeCsv(string.Empty));
        Assert.Equal("\"Nguyễn, \"\"An\"\"\nTầng 2\"", CsvHelper.EscapeCsv("Nguyễn, \"An\"\nTầng 2"));

        // CSV injection: công thức bị vô hiệu bằng ' ở đầu, số âm/số điện thoại có + vẫn giữ nguyên
        Assert.Equal("\"'=HYPERLINK(\"\"http://x\"\")\"", CsvHelper.EscapeCsv("=HYPERLINK(\"http://x\")"));
        Assert.Equal("\"'@SUM(A1)\"", CsvHelper.EscapeCsv("@SUM(A1)"));
        Assert.Equal("\"'-1+cmd|' /C calc'!A0\"", CsvHelper.EscapeCsv("-1+cmd|' /C calc'!A0"));
        Assert.Equal("\"-150000\"", CsvHelper.EscapeCsv("-150000"));
        Assert.Equal("\"+84912345678\"", CsvHelper.EscapeCsv("+84912345678"));

        var bytes = CsvHelper.ToUtf8WithBom("Tiếng Việt");
        Assert.True(bytes.Length >= 3);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("Tiếng Việt", Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public async Task HandoverAndReturnCsv_ProduceExpectedHeadersEscapeSpecialCharsAndFormatVnTime()
    {
        var handoverRow = new AdminRentalScheduleRow
        {
            OrderId = Guid.NewGuid(),
            OrderCode = "RENT-001",
            CustomerName = "Nguyễn, \"An\"\nKho 2",
            CustomerPhone = "0901234567",
            ProductName = "Lều \"2\"",
            VariantName = "Chống, mưa",
            AssetCode = "TENT-01",
            ScheduledAt = new DateTime(2026, 11, 8, 16, 30, 0, DateTimeKind.Utc), // 23:30 08/11 VN
            Status = AssignmentStatuses.Reserved
        };
        var returnRow = new AdminRentalScheduleRow
        {
            OrderId = handoverRow.OrderId,
            OrderCode = "RENT-002",
            CustomerName = "Trần \"Bình\"",
            CustomerPhone = null,
            ProductName = "Gậy leo núi",
            VariantName = "Carbon",
            AssetCode = "POLE-09",
            ScheduledAt = new DateTime(2026, 11, 8, 1, 15, 0, DateTimeKind.Utc), // 08:15 08/11 VN
            Status = AssignmentStatuses.Issued
        };

        var service = CreateFulfillmentService([handoverRow], [returnRow], FixedUtc);
        var targetDate = new DateOnly(2026, 11, 8);

        var (handoverBytes, handoverFileName) = await service.ExportHandoverListCsvAsync(targetDate);
        Assert.Equal("handover-2026-11-08.csv", handoverFileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], handoverBytes[..3]);
        var handoverCsv = Encoding.UTF8.GetString(handoverBytes[3..]);
        _ = handoverCsv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("MaDon,TenKhach,SDT,SanPham,BienThe,MaTaiSan,GioNhan,TrangThai", handoverCsv);
        Assert.Contains("\"RENT-001\",\"Nguyễn, \"\"An\"\"\nKho 2\",\"0901234567\",\"Lều \"\"2\"\"\",\"Chống, mưa\",\"TENT-01\",\"08/11/2026 23:30\",\"Reserved\"", handoverCsv);

        var (returnBytes, returnFileName) = await service.ExportReturnListCsvAsync(targetDate);
        Assert.Equal("return-2026-11-08.csv", returnFileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], returnBytes[..3]);
        var returnCsv = Encoding.UTF8.GetString(returnBytes[3..]);
        Assert.StartsWith("MaDon,TenKhach,SDT,SanPham,BienThe,MaTaiSan,GioTra,TrangThai", returnCsv);
        Assert.Contains("\"RENT-002\",\"Trần \"\"Bình\"\"\",,\"Gậy leo núi\",\"Carbon\",\"POLE-09\",\"08/11/2026 08:15\",\"Issued\"", returnCsv);
    }

    [Fact]
    public async Task HandoverAndReturnSchedule_FilterByVietnamUtcPlus7DayBoundary()
    {
        var lateNov8VnUtc = new DateTime(2026, 11, 8, 16, 30, 0, DateTimeKind.Utc); // 2026-11-08 23:30 +07:00
        var midnightNov9VnUtc = new DateTime(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc); // 2026-11-09 00:00 +07:00

        var handovers = new List<AdminRentalScheduleRow>
        {
            new() { OrderCode = "NOV8-LATE", AssetCode = "A-01", ScheduledAt = lateNov8VnUtc, Status = AssignmentStatuses.Reserved },
            new() { OrderCode = "NOV9-EARLY", AssetCode = "A-02", ScheduledAt = midnightNov9VnUtc, Status = AssignmentStatuses.Reserved }
        };
        var returns = new List<AdminRentalScheduleRow>
        {
            new() { OrderCode = "RET-NOV8", AssetCode = "R-01", ScheduledAt = lateNov8VnUtc, Status = AssignmentStatuses.Issued },
            new() { OrderCode = "RET-NOV9", AssetCode = "R-02", ScheduledAt = midnightNov9VnUtc, Status = AssignmentStatuses.Issued }
        };

        var service = CreateFulfillmentService(handovers, returns, FixedUtc);

        var nov8 = await service.GetHandoverListAsync(new DateOnly(2026, 11, 8));
        Assert.Equal(new DateOnly(2026, 11, 8), nov8.Date);
        Assert.Equal("NOV8-LATE", Assert.Single(nov8.Items).OrderCode);

        var nov9 = await service.GetHandoverListAsync(new DateOnly(2026, 11, 9));
        Assert.Equal(new DateOnly(2026, 11, 9), nov9.Date);
        Assert.Equal("NOV9-EARLY", Assert.Single(nov9.Items).OrderCode);

        // Bỏ trống date -> mặc định ngày hiện tại theo UTC+7 (2026-11-09 dù UTC vẫn là 18:00 ngày 08/11)
        var returnTodayVn = await service.GetReturnListAsync(null);
        Assert.Equal(new DateOnly(2026, 11, 9), returnTodayVn.Date);
        Assert.Equal("RET-NOV9", Assert.Single(returnTodayVn.Items).OrderCode);
    }

    [Fact]
    public void ReportCsv_FormatsRevenueAndRefundChargesFromExistingJsonData()
    {
        var reportService = new AdminReportService(
            TestProxy.Create<IUnitOfWork>(),
            new TestActor(Guid.NewGuid(), RoleNames.Admin),
            new FixedClock(FixedUtc));

        var revenueRows = new List<AdminRevenueResponse>
        {
            new(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), "TripBooking", 3_000_000m, 500_000m, 2_500_000m),
            new(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), "Rental", 1_200_000m, 0m, 1_200_000m)
        };

        var (revBytes, revFileName) = reportService.FormatRevenueCsv(revenueRows);
        Assert.Equal("revenue-2026-11-09.csv", revFileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], revBytes[..3]);
        var revCsv = Encoding.UTF8.GetString(revBytes[3..]);
        Assert.StartsWith("KyBaoCao,LoaiDonHang,TongThuVnd,TongHoanVnd,DoanhThuRongVnd", revCsv);
        Assert.Contains("\"2026-11-01\",\"TripBooking\",\"3000000\",\"500000\",\"2500000\"", revCsv);
        Assert.Contains("\"2026-11-01\",\"Rental\",\"1200000\",\"0\",\"1200000\"", revCsv);

        var (refBytes, refFileName) = reportService.FormatRefundChargesCsv(new AdminRefundChargeResponse(500_000m, 150_000m));
        Assert.Equal("refunds-and-charges-2026-11-09.csv", refFileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], refBytes[..3]);
        var refCsv = Encoding.UTF8.GetString(refBytes[3..]);
        Assert.StartsWith("TongTienHoanVnd,TongPhuPhiThuDuocVnd", refCsv);
        Assert.Contains("\"500000\",\"150000\"", refCsv);
    }

    [Fact]
    public void ReportCsv_WhenNonAdmin_ThrowsForbidden()
    {
        var staffService = new AdminReportService(
            TestProxy.Create<IUnitOfWork>(),
            new TestActor(Guid.NewGuid(), RoleNames.Staff),
            new FixedClock(FixedUtc));

        Assert.Throws<ForbiddenException>(() => staffService.FormatRevenueCsv([]));
        Assert.Throws<ForbiddenException>(() => staffService.FormatRefundChargesCsv(new AdminRefundChargeResponse(0m, 0m)));
    }

    [AdminPostgresFact]
    public async Task RentalScheduleQueries_Postgres_RespectVnDateBoundaryStatusesAndAssignments()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var context = database.Context();

        var existingRental = await context.Orders
            .Include(o => o.RentalOrder!).ThenInclude(r => r.RentalOrderItems).ThenInclude(i => i.AssetAssignments)
            .SingleAsync(o => o.OrderCode == "RENT-TEST");
        existingRental.Status = OrderStatuses.Confirmed;
        existingRental.RentalOrder!.RentalStartAt = new DateTime(2026, 11, 8, 16, 30, 0, DateTimeKind.Utc); // 23:30 8/11 VN
        existingRental.RentalOrder.RentalEndAt = new DateTime(2026, 11, 10, 16, 30, 0, DateTimeKind.Utc);
        var existingAssignment = existingRental.RentalOrder.RentalOrderItems.SelectMany(i => i.AssetAssignments).Single();
        existingAssignment.Status = AssignmentStatuses.Reserved;
        await context.SaveChangesAsync();

        var fulfillment = database.Fulfillment(context, new FixedClock(FixedUtc), database.StaffId, RoleNames.Staff);

        var nov8Handover = await fulfillment.GetHandoverListAsync(new DateOnly(2026, 11, 8));
        var item = Assert.Single(nov8Handover.Items);
        Assert.Equal("RENT-TEST", item.OrderCode);
        Assert.Equal("TENT-001", item.AssetCode);
        Assert.Equal(AssignmentStatuses.Reserved, item.Status);

        var nov9Handover = await fulfillment.GetHandoverListAsync(new DateOnly(2026, 11, 9));
        Assert.Empty(nov9Handover.Items);

        // Chuyển sang InFulfillment + Issued chưa trả để kiểm return-list ở 2026-11-10 VN
        existingRental.Status = OrderStatuses.InFulfillment;
        existingAssignment.Status = AssignmentStatuses.Issued;
        existingAssignment.ReturnedAt = null;
        await context.SaveChangesAsync();

        var nov10Return = await fulfillment.GetReturnListAsync(new DateOnly(2026, 11, 10));
        var retItem = Assert.Single(nov10Return.Items);
        Assert.Equal("RENT-TEST", retItem.OrderCode);
        Assert.Equal(AssignmentStatuses.Issued, retItem.Status);

        var nov11Return = await fulfillment.GetReturnListAsync(new DateOnly(2026, 11, 11));
        Assert.Empty(nov11Return.Items);
    }

    private static RentalFulfillmentService CreateFulfillmentService(
        IReadOnlyList<AdminRentalScheduleRow> handovers,
        IReadOnlyList<AdminRentalScheduleRow> returns,
        DateTimeOffset now)
    {
        var repo = TestProxy.Create<IRentalOrderRepository>(
            ("GetHandoverScheduleAsync", args =>
            {
                var start = (DateTime)args[0]!;
                var end = (DateTime)args[1]!;
                IReadOnlyList<AdminRentalScheduleRow> filtered = handovers.Where(x => x.ScheduledAt >= start && x.ScheduledAt < end).ToList();
                return Task.FromResult(filtered);
            }
        ),
            ("GetReturnScheduleAsync", args =>
            {
                var start = (DateTime)args[0]!;
                var end = (DateTime)args[1]!;
                IReadOnlyList<AdminRentalScheduleRow> filtered = returns.Where(x => x.ScheduledAt >= start && x.ScheduledAt < end).ToList();
                return Task.FromResult(filtered);
            }
        ));
        var uow = TestProxy.Create<IUnitOfWork>(("get_RentalOrders", _ => repo));
        return new RentalFulfillmentService(uow, new TestActor(Guid.NewGuid(), RoleNames.Staff), new FixedClock(now));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestActor(Guid userId, string role) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid RequiredUserId => userId;
        public string? Jti => null;
        public string? Role => role;
        public bool IsAuthenticated => true;
    }
}
