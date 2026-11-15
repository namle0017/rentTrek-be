using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using RenTrek.API.Controllers;
using RenTrek.BLL.Options;
using RenTrek.BLL.Tests;
using RenTrek.DAL.Data;

namespace RenTrek.API.Tests;

/// <summary>
/// Phân quyền endpoint chứng từ hoàn tiền / nhập kết quả. Test HTTP cần Postgres (RENTREK_TEST_POSTGRES);
/// test route bằng reflection chạy luôn mà không cần DB.
/// Lưu ý: AdminHttpTests.AdminRouteCount_IsStable (187) và EveryAdminRoute_... (186) phải được cập nhật vì có thêm route admin.
/// </summary>
public sealed class RefundProofHttpTests
{
    private static IEnumerable<(string Verb, string Path, MethodInfo Method)> AllRoutes()
    {
        foreach (var controller in typeof(AdminRefundCasesController).Assembly.GetTypes()
                     .Where(x => typeof(ControllerBase).IsAssignableFrom(x) && !x.IsAbstract))
        {
            var root = controller.GetCustomAttribute<RouteAttribute>()?.Template;
            if (root is null) continue;
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var route in method.GetCustomAttributes<HttpMethodAttribute>())
                    foreach (var verb in route.HttpMethods)
                        yield return (verb, root + (string.IsNullOrEmpty(route.Template) ? "" : "/" + route.Template), method);
        }
    }

    [Fact]
    public void Routes_ImportResultAndProofEndpointsExist()
    {
        var routes = AllRoutes().ToList();

        Assert.Contains(routes, r => r.Verb == "POST" && r.Path == "api/v1/admin/refund-cases/import-result");
        Assert.Contains(routes, r => r.Verb == "GET" && r.Path.StartsWith("api/v1/admin/refund-cases/{refundCaseId", StringComparison.Ordinal) && r.Path.Contains("/proofs/{proofId", StringComparison.Ordinal));
        Assert.Contains(routes, r => r.Verb == "GET" && r.Path.StartsWith("api/v1/refund-cases/{refundCaseId", StringComparison.Ordinal) && r.Path.Contains("/proofs/{proofId", StringComparison.Ordinal));
        Assert.Contains(routes, r => r.Verb == "GET" && r.Path.StartsWith("api/v1/admin/refunds/{paymentId", StringComparison.Ordinal) && r.Path.Contains("/proofs/{proofId", StringComparison.Ordinal));
    }

    [Fact]
    public void Routes_CompleteAndMarkManualAcceptMultipart()
    {
        var routes = AllRoutes().ToList();

        var complete = routes.Single(r => r.Verb == "POST" && r.Path.StartsWith("api/v1/admin/refund-cases/{refundCaseId", StringComparison.Ordinal) && r.Path.EndsWith("/complete", StringComparison.Ordinal));
        var markManual = routes.Single(r => r.Verb == "POST" && r.Path.EndsWith("/mark-manual", StringComparison.Ordinal));
        var import = routes.Single(r => r.Verb == "POST" && r.Path.EndsWith("/import-result", StringComparison.Ordinal));
        foreach (var route in new[] { complete, markManual, import })
        {
            var consumes = route.Method.GetCustomAttribute<ConsumesAttribute>();
            Assert.NotNull(consumes);
            Assert.Contains("multipart/form-data", consumes!.ContentTypes);
        }
    }

    [Fact]
    public void Routes_AdminProofAndImportEndpointsRequireAdminRole()
    {
        foreach (var route in AllRoutes().Where(r => r.Path.StartsWith("api/v1/admin/", StringComparison.Ordinal)
                                                     && (r.Path.Contains("/proofs/", StringComparison.Ordinal) || r.Path.EndsWith("/import-result", StringComparison.Ordinal))))
        {
            var authorize = route.Method.DeclaringType!.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
                .Concat(route.Method.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()).ToList();
            Assert.Contains(authorize, a => a.Roles is not null && a.Roles.Contains("Admin", StringComparison.Ordinal));
            Assert.Null(route.Method.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
        }
    }

    [AdminPostgresFact]
    public async Task AdminEndpoints_RejectAnonymousCustomerAndStaff()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var customer = await LoggedInAsync(factory, database.CustomerId);
        using var staff = await LoggedInAsync(factory, database.StaffId);
        var caseId = Guid.NewGuid();
        var proofId = Guid.NewGuid();

        foreach (var (verb, path, multipart) in new (string, string, bool)[]
                 {
                     ("POST", "/api/v1/admin/refund-cases/import-result", true),
                     ("POST", $"/api/v1/admin/refund-cases/{caseId}/complete", true),
                     ("GET", $"/api/v1/admin/refund-cases/{caseId}/proofs/{proofId}", false),
                     ("POST", $"/api/v1/admin/refunds/{Guid.NewGuid()}/mark-manual", true),
                     ("GET", $"/api/v1/admin/refunds/{Guid.NewGuid()}/proofs/{proofId}", false)
                 })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(anonymous, verb, path, multipart)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(customer, verb, path, multipart)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(staff, verb, path, multipart)).StatusCode);
        }
    }

    [AdminPostgresFact]
    public async Task CustomerProofEndpoint_RejectsAnonymous_AndHidesUnknownOrForeignCases()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var customer = await LoggedInAsync(factory, database.CustomerId);
        var path = $"/api/v1/refund-cases/{Guid.NewGuid()}/proofs/{Guid.NewGuid()}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        // Không phải chủ hồ sơ (hoặc hồ sơ không tồn tại) -> 404, không lộ sự tồn tại của hồ sơ
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync(path)).StatusCode);
    }

    [AdminPostgresFact]
    public async Task AdminProofEndpoint_UnknownProof_Returns404()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = await LoggedInAsync(factory, database.AdminId);

        using var response = await admin.GetAsync($"/api/v1/admin/refund-cases/{Guid.NewGuid()}/proofs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [AdminPostgresFact]
    public async Task ImportResult_NonXlsxFile_Returns400()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = await LoggedInAsync(factory, database.AdminId);
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes("not an excel file")), "file", "ketqua.txt" }
        };

        using var response = await admin.PostAsync("/api/v1/admin/refund-cases/import-result", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
    }

    [AdminPostgresFact]
    public async Task ImportResult_WithoutFile_Returns400()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = await LoggedInAsync(factory, database.AdminId);
        using var content = new MultipartFormDataContent { { new StringContent("true"), "dryRun" } };

        using var response = await admin.PostAsync("/api/v1/admin/refund-cases/import-result", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [AdminPostgresFact]
    public async Task Complete_WithoutFiles_Returns400OrNotFoundButNeverSucceeds()
    {
        await using var database = new AdminTestDatabase();
        await database.InitializeAsync();
        await using var factory = new Factory(database.ConnectionString);
        using var admin = await LoggedInAsync(factory, database.AdminId);
        using var content = new MultipartFormDataContent
        {
            { new StringContent("BANK-1"), "providerTransactionId" },
            { new StringContent("1"), "expectedRowVersion" }
        };

        using var response = await admin.PostAsync($"/api/v1/admin/refund-cases/{Guid.NewGuid()}/complete", content);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, $"Got {(int)response.StatusCode}");
    }

    // ---------------------------------------------------------------- helpers

    private static Task<HttpResponseMessage> Send(HttpClient client, string verb, string path, bool multipart)
    {
        var request = new HttpRequestMessage(new HttpMethod(verb), path);
        if (verb != "GET")
            request.Content = multipart ? new MultipartFormDataContent { { new StringContent("x"), "x" } } : JsonContent.Create(new { });
        return client.SendAsync(request);
    }

    private static async Task<HttpClient> LoggedInAsync(Factory factory, Guid userId)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = userId + "@test.invalid", password = "<TestPassword123!>" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope.GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }

    // Bản sao Factory của AdminHttpTests (lớp đó private).
    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        private const string Key = "<disposable-admin-http-tests-only-key-at-least-32-characters>";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:SecretKey"] = Key,
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
                    options.SecretKey = Key;
                    options.Issuer = "rentrek-tests";
                    options.Audience = "rentrek-tests";
                    options.AccessTokenExpirationMinutes = 15;
                    options.RefreshTokenExpirationDays = 7;
                });
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
                    options.TokenValidationParameters.ValidIssuer = "rentrek-tests";
                    options.TokenValidationParameters.ValidAudience = "rentrek-tests";
                });
            });
        }
    }
}
