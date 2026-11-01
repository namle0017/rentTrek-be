using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Data;

namespace RenTrek.IntegrationTests;

public sealed class ApiFactory(PostgresFixture fixture) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        var settings = new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString, ["Jwt:SecretKey"] = "test-only-secret-at-least-32-characters", ["SePay:BankName"] = "MBBank", ["SePay:AccountNumber"] = "0123456789", ["SePay:AccountName"] = "RENTREK", ["SePay:Api:Key"] = "test-only-key", ["SePay:AuthMode"] = "ApiKey" };
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AppDbContext>(); services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
            services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new TestClock());
            services.RemoveAll<IPaymentDataProtector>(); services.AddSingleton<IPaymentDataProtector, TestProtector>();
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "test"; options.DefaultChallengeScheme = "test"; options.DefaultForbidScheme = "test"; })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        });
    }
    public HttpClient Client(Guid userId, string role)
    {
        var client = CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString()); client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }
}

public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Test-User"], out var user)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString()) };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), "test")));
    }
}

[Collection("postgres")]
public sealed class ManualRefundContractTests(PostgresFixture fixture)
{
    [Fact]
    public async Task CustomerCannotUseStaffQueue()
    {
        var seed = await fixture.SeedOrderAsync(); await using var factory = new ApiFactory(fixture); using var client = factory.Client(seed.CustomerId, "Customer");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/staff/refund-requests")).StatusCode);
    }

    [Fact]
    public async Task CustomerToStaffManualRefund_WorksThroughHttpContract()
    {
        var seed = await new CustomerRefundRequestTests(fixture).PaidOrder(); await using var factory = new ApiFactory(fixture);
        using var customer = factory.Client(seed.CustomerId, "Customer"); customer.DefaultRequestHeaders.Add("Idempotency-Key", "http-request");
        var response = await customer.PostAsJsonAsync($"/api/v1/orders/{seed.OrderId}/refund-requests", CustomerRefundRequestTests.Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); var id = json.GetProperty("data").GetProperty("id").GetGuid();
        using var staff = factory.Client(seed.StaffId, "Staff");
        var approval = await staff.PostAsJsonAsync($"/api/v1/staff/refund-requests/{id}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        staff.DefaultRequestHeaders.Add("Idempotency-Key", "http-done");
        var done = await staff.PostAsJsonAsync($"/api/v1/staff/refund-requests/{id}/done", new { transferReference = $"HTTP-{id:N}", transferredAt = TestClock.Now });
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var detail = await customer.GetFromJsonAsync<JsonElement>($"/api/v1/refund-requests/{id}");
        Assert.Equal("Done", detail.GetProperty("data").GetProperty("status").GetString());
    }
}
