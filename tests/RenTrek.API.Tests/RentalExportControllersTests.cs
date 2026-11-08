using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.API.Common;
using RenTrek.API.Controllers;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.RentalOps.Responses;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Tests;

public sealed class RentalExportControllersTests
{
    [Fact]
    public void Controllers_PreserveExpectedAuthorizationRoles()
    {
        var rentalAuth = Assert.Single(
            typeof(AdminRentalOrdersController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Equal("Admin,Staff", rentalAuth.Roles);

        var reportsClassAuth = Assert.Single(
            typeof(AdminReportsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Equal("Admin,Staff", reportsClassAuth.Roles);

        var revenueAuth = Assert.Single(
            typeof(AdminReportsController).GetMethod(nameof(AdminReportsController.RevenueAsync))!
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Equal(RoleNames.Admin, revenueAuth.Roles);

        var refundsAuth = Assert.Single(
            typeof(AdminReportsController).GetMethod(nameof(AdminReportsController.RefundChargesAsync))!
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Equal(RoleNames.Admin, refundsAuth.Roles);
    }

    [Fact]
    public async Task ExportControllers_SupportJsonAndCsv_AndReportsReuseSingleQuery()
    {
        var fulfillment = new StubRentalFulfillmentService();
        var rentalController = new AdminRentalOrdersController(fulfillment);
        var date = new DateOnly(2026, 11, 8);

        var handoverJson = await rentalController.GetHandoverListAsync(date, null, CancellationToken.None);
        var handoverOk = Assert.IsType<OkObjectResult>(handoverJson);
        Assert.IsType<ApiResponse<AdminRentalScheduleResponse>>(handoverOk.Value);

        var handoverCsv = await rentalController.GetHandoverListAsync(date, "csv", CancellationToken.None);
        var handoverFile = Assert.IsType<FileContentResult>(handoverCsv);
        Assert.Equal("text/csv; charset=utf-8", handoverFile.ContentType);
        Assert.Equal("handover-2026-11-08.csv", handoverFile.FileDownloadName);

        var returnJson = await rentalController.GetReturnListAsync(date, null, CancellationToken.None);
        var returnOk = Assert.IsType<OkObjectResult>(returnJson);
        Assert.IsType<ApiResponse<AdminRentalScheduleResponse>>(returnOk.Value);

        var returnCsv = await rentalController.GetReturnListAsync(date, "csv", CancellationToken.None);
        var returnFile = Assert.IsType<FileContentResult>(returnCsv);
        Assert.Equal("text/csv; charset=utf-8", returnFile.ContentType);
        Assert.Equal("return-2026-11-08.csv", returnFile.FileDownloadName);

        var reportService = new StubAdminReportService();
        var reportsController = new AdminReportsController(reportService);

        var revCsv = await reportsController.RevenueAsync(new AdminQueryRequest(), "csv", CancellationToken.None);
        var revFile = Assert.IsType<FileContentResult>(revCsv);
        Assert.Equal("text/csv; charset=utf-8", revFile.ContentType);
        Assert.Equal("revenue-2026-11-08.csv", revFile.FileDownloadName);
        Assert.Equal(1, reportService.RevenueQueryCount);
        Assert.Equal(1, reportService.RevenueFormatCount);

        var refCsv = await reportsController.RefundChargesAsync(new AdminQueryRequest(), "csv", CancellationToken.None);
        var refFile = Assert.IsType<FileContentResult>(refCsv);
        Assert.Equal("text/csv; charset=utf-8", refFile.ContentType);
        Assert.Equal("refunds-and-charges-2026-11-08.csv", refFile.FileDownloadName);
        Assert.Equal(1, reportService.RefundQueryCount);
        Assert.Equal(1, reportService.RefundFormatCount);
    }

    [Fact]
    public async Task HttpPipeline_EnforcesCustomer403AndStaffReportCsv403_WhileServingCsv()
    {
        await using var factory = new TestApiFactory();

        using var anonymous = factory.CreateClient();
        using var customer = factory.CreateClient();
        customer.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.Customer);
        using var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.Staff);
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.Admin);

        var endpoints = new[]
        {
            "/api/v1/admin/rental-orders/handover-list?date=2026-11-08&format=csv",
            "/api/v1/admin/rental-orders/return-list?date=2026-11-08&format=csv",
            "/api/v1/admin/reports/revenue?format=csv",
            "/api/v1/admin/reports/refunds-and-charges?format=csv"
        };

        foreach (var endpoint in endpoints)
        {
            using var anonResponse = await anonymous.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.Unauthorized, anonResponse.StatusCode);

            using var customerResponse = await customer.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        }

        // Staff tải được lịch giao/nhận, nhưng report vẫn Admin-only -> 403
        using var staffHandover = await staff.GetAsync("/api/v1/admin/rental-orders/handover-list?date=2026-11-08&format=csv");
        Assert.Equal(HttpStatusCode.OK, staffHandover.StatusCode);
        Assert.Equal("text/csv", staffHandover.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", staffHandover.Content.Headers.ContentType?.CharSet);

        using var staffReturn = await staff.GetAsync("/api/v1/admin/rental-orders/return-list?date=2026-11-08&format=csv");
        Assert.Equal(HttpStatusCode.OK, staffReturn.StatusCode);
        Assert.Equal("text/csv", staffReturn.Content.Headers.ContentType?.MediaType);

        using var staffRev = await staff.GetAsync("/api/v1/admin/reports/revenue?format=csv");
        Assert.Equal(HttpStatusCode.Forbidden, staffRev.StatusCode);

        using var staffRef = await staff.GetAsync("/api/v1/admin/reports/refunds-and-charges?format=csv");
        Assert.Equal(HttpStatusCode.Forbidden, staffRef.StatusCode);

        // Admin tải được cả 2 report CSV
        using var adminRev = await admin.GetAsync("/api/v1/admin/reports/revenue?format=csv");
        Assert.Equal(HttpStatusCode.OK, adminRev.StatusCode);
        Assert.Equal("text/csv", adminRev.Content.Headers.ContentType?.MediaType);

        using var adminRef = await admin.GetAsync("/api/v1/admin/reports/refunds-and-charges?format=csv");
        Assert.Equal(HttpStatusCode.OK, adminRef.StatusCode);
        Assert.Equal("text/csv", adminRef.Content.Headers.ContentType?.MediaType);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused;Username=unused",
                ["Jwt:SecretKey"] = "unit-test-only-signing-key-at-least-32-characters",
                ["Jwt:Issuer"] = "rentrek-tests",
                ["Jwt:Audience"] = "rentrek-tests",
                ["RefundDataEncryptionKey"] = Convert.ToBase64String(new byte[32]),
                ["Logging:LogLevel:Default"] = "None"
            })
                builder.UseSetting(key, value);

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = HeaderAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = HeaderAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.SchemeName, _ => { });

                services.Replace(ServiceDescriptor.Scoped<IRentalFulfillmentService, StubRentalFulfillmentService>());
                services.Replace(ServiceDescriptor.Scoped<IAdminReportService, StubAdminReportService>());
            });
        }
    }

    private sealed class HeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeader";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var roleHeader) || string.IsNullOrWhiteSpace(roleHeader))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, roleHeader.ToString())
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class StubRentalFulfillmentService : IRentalFulfillmentService
    {
        public Task<AdminRentalScheduleResponse> GetHandoverListAsync(DateOnly? date, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminRentalScheduleResponse { Date = date ?? new DateOnly(2026, 11, 8) });

        public Task<(byte[] FileBytes, string FileName)> ExportHandoverListCsvAsync(DateOnly? date, CancellationToken cancellationToken = default)
            => Task.FromResult((Encoding.UTF8.GetBytes("MaDon,TenKhach\r\n"), $"handover-{(date ?? new DateOnly(2026, 11, 8)):yyyy-MM-dd}.csv"));

        public Task<AdminRentalScheduleResponse> GetReturnListAsync(DateOnly? date, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminRentalScheduleResponse { Date = date ?? new DateOnly(2026, 11, 8) });

        public Task<(byte[] FileBytes, string FileName)> ExportReturnListCsvAsync(DateOnly? date, CancellationToken cancellationToken = default)
            => Task.FromResult((Encoding.UTF8.GetBytes("MaDon,TenKhach\r\n"), $"return-{(date ?? new DateOnly(2026, 11, 8)):yyyy-MM-dd}.csv"));

        public Task<BLL.DTOs.Common.Responses.PagedResponse<AdminRentalOrderListItemResponse>> GetListAsync(BLL.DTOs.RentalOps.Requests.AdminRentalOrderQueryRequest query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> ClaimAsync(Guid orderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> ScanHandoverAsync(Guid orderId, BLL.DTOs.RentalOps.Requests.ScanHandoverRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> HandoverAsync(Guid orderId, BLL.DTOs.RentalOps.Requests.HandoverRentalRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> ReturnAsync(Guid orderId, BLL.DTOs.RentalOps.Requests.ReturnRentalAssetsRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> CreateChargeAsync(Guid orderId, BLL.DTOs.RentalOps.Requests.CreateRentalChargeRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> AssessChargeAsync(Guid orderId, Guid chargeId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> VoidChargeAsync(Guid orderId, Guid chargeId, BLL.DTOs.RentalOps.Requests.VoidRentalChargeRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<AdminRentalOrderDetailResponse> CompleteAsync(Guid orderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<List<BLL.DTOs.RentalOrder.Responses.RentalOrderAdjustmentResponse>> GetAdjustmentsAsync(Guid orderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubAdminReportService : IAdminReportService
    {
        public int RevenueQueryCount { get; private set; }
        public int RevenueFormatCount { get; private set; }
        public int RefundQueryCount { get; private set; }
        public int RefundFormatCount { get; private set; }

        public Task<List<AdminRevenueResponse>> GetRevenueAsync(AdminQueryRequest request, CancellationToken cancellationToken)
        {
            RevenueQueryCount++;
            return Task.FromResult(new List<AdminRevenueResponse>());
        }

        public Task<AdminRefundChargeResponse> GetRefundChargesAsync(AdminQueryRequest request, CancellationToken cancellationToken)
        {
            RefundQueryCount++;
            return Task.FromResult(new AdminRefundChargeResponse(100_000m, 20_000m));
        }

        public (byte[] FileBytes, string FileName) FormatRevenueCsv(IReadOnlyList<AdminRevenueResponse> items)
        {
            RevenueFormatCount++;
            return (Encoding.UTF8.GetBytes("KyBaoCao\r\n"), "revenue-2026-11-08.csv");
        }

        public (byte[] FileBytes, string FileName) FormatRefundChargesCsv(AdminRefundChargeResponse report)
        {
            RefundFormatCount++;
            return (Encoding.UTF8.GetBytes("TongTienHoanVnd\r\n"), "refunds-and-charges-2026-11-08.csv");
        }

        public Task<AdminDashboardStatsResponse> GetStatsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<AdminAlertResponse>> GetAlertsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<AdminKpisResponse> GetKpisAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<AdminOccupancyResponse>> GetOccupancyAsync(AdminQueryRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<AdminUtilizationResponse>> GetUtilizationAsync(AdminQueryRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
