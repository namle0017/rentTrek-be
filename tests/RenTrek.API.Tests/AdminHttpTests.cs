using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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
            foreach (var method in controller.GetMethods())
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
        Assert.Equal(108, count);
    }

    [AdminPostgresFact]
    public async Task AdminAndStaff_RespectRoutesAndDataScope_AndRevokedTokensFailImmediately()
    {
        await using var database = new AdminTestDatabase(); await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var staff = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await LoginAsync(admin, database.AdminId); await LoginAsync(staff, database.StaffId);
        foreach (var endpoint in new[] { "users", "categories", "locations", "tags", "soft-skills", "orders", "rental-orders", "promotions", "posts", "settings", "audit-logs", "reviews", "dashboard/stats", "dashboard/alerts", "dashboard/kpis", "reports/revenue", "reports/trip-occupancy", "reports/asset-utilization", "reports/refunds-and-charges", "orders/TOUR-TEST" })
        {
            using var response = await admin.GetAsync("/api/v1/admin/" + endpoint);
            Assert.True(response.IsSuccessStatusCode, endpoint + ": " + await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/dashboard/kpis")).StatusCode);
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
        using var created = await admin.PostAsJsonAsync("/api/v1/admin/users/staff", new { email = "guide-new@test.invalid", fullName = "Guide mới", phone = "0912345678", role = "Guide", password = "TestPassword123!", careerStartDate = "2020-01-01" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var envelope = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(201, envelope.GetProperty("statusCode").GetInt32());
        Assert.Equal("Guide", envelope.GetProperty("data").GetProperty("user").GetProperty("role").GetString());
        using var invalid = await admin.PostAsJsonAsync("/api/v1/admin/users/staff", new { email = "bad", fullName = "Guide mới", role = "Guide", password = "weak" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var error = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.GetProperty("errors").TryGetProperty("email", out _));
    }

    private static async Task LoginAsync(HttpClient client, Guid userId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = userId + "@test.invalid", password = "TestPassword123!" });
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
                ["Jwt:SecretKey"] = "disposable-admin-http-tests-only-key-at-least-32-characters",
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
                    options.SecretKey = "disposable-admin-http-tests-only-key-at-least-32-characters";
                    options.Issuer = "rentrek-tests"; options.Audience = "rentrek-tests";
                    options.AccessTokenExpirationMinutes = 15; options.RefreshTokenExpirationDays = 7;
                });
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("disposable-admin-http-tests-only-key-at-least-32-characters"));
                    options.TokenValidationParameters.ValidIssuer = "rentrek-tests";
                    options.TokenValidationParameters.ValidAudience = "rentrek-tests";
                });
            });
        }
    }
}
