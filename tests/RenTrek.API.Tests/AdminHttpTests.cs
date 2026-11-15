using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RenTrek.BLL.Options;
using System.Text;
using RenTrek.API.Controllers;
using RenTrek.BLL.Tests;

namespace RenTrek.API.Tests;

public sealed class AdminHttpTests
{
    [Fact]
    public void AdminRouteCount_IsStable()
    {
        var controllers = typeof(AdminOrdersController).Assembly.GetTypes().Where(x => x.Namespace == "RenTrek.API.Controllers" && x.Name.StartsWith("Admin", StringComparison.Ordinal) && typeof(ControllerBase).IsAssignableFrom(x));
        var count = controllers.SelectMany(c => c.GetMethods()).SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()).Sum(r => r.HttpMethods.Count());
        Assert.Equal(193, count); // gồm 1 route AllowAnonymous (/api/v1/rental-policies/active) được loại khỏi test 401/403
    }

    [AdminPostgresFact]
    public async Task EveryAdminRoute_RejectsAnonymousAndCustomer_WithApiResponse()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var customer = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(customer, database.CustomerId);
        var controllers = typeof(AdminOrdersController).Assembly.GetTypes().Where(x => x.Namespace == "RenTrek.API.Controllers" && x.Name.StartsWith("Admin", StringComparison.Ordinal) && typeof(ControllerBase).IsAssignableFrom(x));
        var count = 0;
        foreach (var controller in controllers)
        {
            var root = controller.GetCustomAttribute<RouteAttribute>()!.Template;
            foreach (var method in controller.GetMethods().Where(m => m.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() is null))
                foreach (var route in method.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var path = root + (string.IsNullOrEmpty(route.Template) ? "" : "/" + route.Template);
                    path = System.Text.RegularExpressions.Regex.Replace(path, @"\{[^}]+:guid\}", Guid.NewGuid().ToString());
                    path = System.Text.RegularExpressions.Regex.Replace(path, @"\{[^}]+\}", "missing-test");
                    // Endpoint [Consumes] (upload) trả 415 trước auth nếu gửi JSON.
                    var multipart = method.GetCustomAttribute<ConsumesAttribute>() is not null;
                    HttpContent Body() => multipart ? new MultipartFormDataContent { { new StringContent("x"), "x" } } : JsonContent.Create(new { });
                    foreach (var verb in route.HttpMethods)
                    {
                        using var unauthorizedRequest = new HttpRequestMessage(new HttpMethod(verb), path) { Content = Body() };
                        using var unauthorized = await anonymous.SendAsync(unauthorizedRequest);
                        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
                        Assert.False((await unauthorized.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
                        using var forbiddenRequest = new HttpRequestMessage(new HttpMethod(verb), path) { Content = Body() };
                        using var forbidden = await customer.SendAsync(forbiddenRequest);
                        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
                        count++;
                    }
                }
        }
        Assert.Equal(189, count);
    }

    [AdminPostgresFact]
    public async Task AdminAndStaff_RespectRoutesAndDataScope_AndRevokedTokensFailImmediately()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var staff = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(admin, database.AdminId); await LoginAsync(staff, database.StaffId);
        foreach (var endpoint in new[] { "users", "categories", "locations", "tags", "soft-skills", "orders", "rental-orders", "rental-orders/handover-list", "rental-orders/return-list", "promotions", "posts", "settings", "audit-logs", "reviews", "dashboard/stats", "dashboard/alerts", "dashboard/kpis", "reports/revenue", "reports/trip-occupancy", "reports/asset-utilization", "reports/refunds-and-charges", "orders/TOUR-TEST" })
        {
            using var response = await admin.GetAsync("/api/v1/admin/" + endpoint);
            Assert.True(response.IsSuccessStatusCode, endpoint + ": " + await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/dashboard/kpis")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/reports/revenue?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/reports/refunds-and-charges?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/rental-orders/handover-list?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/rental-orders/return-list?format=csv")).StatusCode);
        var orders = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders");
        Assert.Equal(1, orders.GetProperty("data").GetProperty("totalItems").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/api/v1/admin/orders/RENT-TEST")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/rental-orders/TOUR-TEST")).StatusCode);
        var clampedPage = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/users?pageSize=101");
        Assert.Equal(100, clampedPage.GetProperty("data").GetProperty("pageSize").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/users?sortOrder=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/admin/users/{database.StaffId}/lock", new { reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/admin/users/{database.StaffId}/lock", new { reason = "Kiểm thử thu hồi phiên" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/admin/dashboard/stats")).StatusCode);
    }

    [AdminPostgresFact]
    public async Task StaffCreation_UsesCreatedEnvelope_AndValidationFieldsAreCamelCase()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); await LoginAsync(admin, database.AdminId);
        using var created = await admin.PostAsJsonAsync("/api/v1/admin/users/staff", new { email = "guide-new@test.invalid", fullName = "Guide mới", phone = "0912345678", role = "Guide", password = "<TestPassword123!>", careerStartDate = "2020-01-01" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var envelope = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(201, envelope.GetProperty("statusCode").GetInt32());
        Assert.Equal("Guide", envelope.GetProperty("data").GetProperty("user").GetProperty("role").GetString());
        using var invalid = await admin.PostAsJsonAsync("/api/v1/admin/users/staff", new { email = "bad", fullName = "Guide mới", role = "Guide", password = "<weak>" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var error = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.GetProperty("errors").TryGetProperty("email", out _));
    }

    [AdminPostgresFact]
    public async Task Policies_StaffCanRead_ButCannotWrite_AndAdminGetsPagedList()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var staff = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(admin, database.AdminId); await LoginAsync(staff, database.StaffId);
        var id = database.PolicyId;

        // TC-RP-P01..P03: Staff đọc được
        using var list = await staff.GetAsync("/api/v1/admin/refund-policies");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var data = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(data.TryGetProperty("items", out var items), "refund-policies phải trả PagedResponse");
        Assert.True(data.GetProperty("totalItems").GetInt32() >= 1);
        Assert.Equal(id, items.EnumerateArray().Select(x => x.GetProperty("policyId").GetGuid()).Single(x => x == id));
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/admin/refund-policies/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/rental-policies")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/refund-policies?status=Active&policyType=Tour&latestOnly=false&sortBy=name&sortOrder=asc")).StatusCode);

        // TC-RP-P04..P06 + E08, F05: Staff không ghi được
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync($"/api/v1/admin/refund-policies/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"/api/v1/admin/refund-policies/{id}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"/api/v1/admin/refund-policies/{id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"/api/v1/admin/refund-policies/{id}/clone-version", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin/refund-policies", new { name = "x", policyType = "Tour", effectiveFrom = "2026-12-01", rules = new[] { new { minHoursBefore = 0, refundPercentage = 0 } }, saveAsDraft = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync($"/api/v1/admin/refund-policies/{id}/rules", new { rules = new[] { new { minHoursBefore = 0, refundPercentage = 0 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin/rental-policies", new { name = "x" })).StatusCode);
    }

    [AdminPostgresFact]
    public async Task Policies_AdminValidationAndConflicts_UseApiEnvelope()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); await LoginAsync(admin, database.AdminId);
        var id = database.PolicyId;

        // TC-RP-C: tạo + rules lưu đủ (201 Created)
        using var created = await admin.PostAsJsonAsync("/api/v1/admin/refund-policies", new
        {
            name = "Chính sách mùa lễ",
            policyType = "Tour",
            description = "Mô tả",
            effectiveFrom = "2026-12-01",
            rules = new object[] { new { minHoursBefore = 72, refundPercentage = 100 }, new { minHoursBefore = 0, refundPercentage = 0 } },
            saveAsDraft = true
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var newId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetGuid();
        var detail = (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/refund-policies/{newId}")).GetProperty("data");
        Assert.Equal(2, detail.GetProperty("rules").GetArrayLength());
        Assert.Equal("Draft", detail.GetProperty("displayStatus").GetString());

        // TC-RP-V22 / E09: rules sai (% tăng khi sát giờ) -> 400
        using var invalid = await admin.PutAsJsonAsync($"/api/v1/admin/refund-policies/{newId}/rules", new { rules = new object[] { new { minHoursBefore = 72, refundPercentage = 10 }, new { minHoursBefore = 0, refundPercentage = 90 } } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.False((await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());

        // TC-RP-Q09: sortBy không hợp lệ -> 400
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/refund-policies?sortBy=evil")).StatusCode);

        // TC-RP-E02 / D02 / LA01: policy đang dùng
        using var edit = await admin.PutAsJsonAsync($"/api/v1/admin/refund-policies/{id}", new { name = "Đổi tên", policyType = "Tour", effectiveFrom = "2026-01-01", rules = new object[] { new { minHoursBefore = 0, refundPercentage = 0 } }, saveAsDraft = false });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/refund-policies/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/admin/refund-policies/{id}/archive", null)).StatusCode);

        // TC-RP-D01: xoá nháp chưa dùng
        Assert.True((await admin.DeleteAsync($"/api/v1/admin/refund-policies/{newId}")).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/refund-policies/{newId}")).StatusCode);
    }

    [AdminPostgresFact]
    public async Task RentalPolicies_PublicActiveHasNoAdminFields_AndAdminListHas()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); await LoginAsync(admin, database.AdminId);

        // TC-RP-P09 / R12: endpoint public không lộ field admin
        using var active = await anonymous.GetAsync("/api/v1/rental-policies/active");
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        var publicData = (await active.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        foreach (var field in new[] { "ordersCount", "createdByName", "createdAt" })
            Assert.False(publicData.TryGetProperty(field, out _), field + " không được lộ ở /rental-policies/active");

        // Admin: tạo version rồi list có field admin
        using var tiers = await admin.GetAsync("/api/v1/admin/rental-policies");
        Assert.Equal(HttpStatusCode.OK, tiers.StatusCode);
        var body = new
        {
            name = "Policy mới", depositPercent = 20, freeCancelLimit = 2, freeCancelWindowDays = 30, overLimitCancelFeePercent = 5,
            returnGraceHours = 2, extensionMaxDaysLate = 2, suspectedLossAfterDays = 7, receiptAutoConfirmHours = 24, minPhotosPerAsset = 1,
            tiers = new object[]
            {
                new { scope = "PickupLate", minDaysLate = 1, percentOfDeposit = 100, isTerminal = true },
                new { scope = "ReturnLate", minDaysLate = 1, percentOfDeposit = 100, isTerminal = true }
            }
        };
        using var created = await admin.PostAsJsonAsync("/api/v1/admin/rental-policies", body);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var list = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/rental-policies")).GetProperty("data");
        var first = list.EnumerateArray().First();
        foreach (var field in new[] { "ordersCount", "createdByName", "createdAt" })
            Assert.True(first.TryGetProperty(field, out _), field + " thiếu ở admin list");
        var id = first.GetProperty("rentalPolicyId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/admin/rental-policies/{id}")).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, Guid userId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = userId + "@test.invalid", password = "<TestPassword123!>" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope.GetProperty("data").GetProperty("accessToken").GetString());
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            // UseSetting thay vì ConfigureAppConfiguration: Program đọc Jwt ngay khi build, trước khi config của factory được áp.
            foreach (var (key, value) in new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:SecretKey"] = "<disposable-admin-http-tests-only-key-at-least-32-characters>",
                ["Jwt:Issuer"] = "rentrek-tests",
                ["Jwt:Audience"] = "rentrek-tests",
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["RefundDataEncryptionKey"] = Convert.ToBase64String(new byte[32]),
                ["Seed:Tours"] = "false",
                ["Logging:LogLevel:Default"] = "None"
            })
                builder.UseSetting(key, value);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
                services.PostConfigure<JwtSettings>(options =>
                {
                    options.SecretKey = "<disposable-admin-http-tests-only-key-at-least-32-characters>";
                    options.Issuer = "rentrek-tests"; options.Audience = "rentrek-tests";
                    options.AccessTokenExpirationMinutes = 15; options.RefreshTokenExpirationDays = 7;
                });
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("<disposable-admin-http-tests-only-key-at-least-32-characters>"));
                    options.TokenValidationParameters.ValidIssuer = "rentrek-tests";
                    options.TokenValidationParameters.ValidAudience = "rentrek-tests";
                });
            });
        }
    }
}
