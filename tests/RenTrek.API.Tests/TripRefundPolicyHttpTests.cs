using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.API.Tests;

public sealed class TripRefundPolicyHttpTests
{
    [Fact]
    public async Task Get_PrefersTripPolicyAndReturnsTermsWithRulesInDescendingOrder()
    {
        var specific = MakePolicy(version: 3);
        await using var factory = new Factory(specific, MakePolicy(version: 4));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync($"/api/v1/trips/{factory.TripId}/refund-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(specific.RefundPolicyId, data.GetProperty("refundPolicyId").GetGuid());
        Assert.Equal("Chính sách Tour", data.GetProperty("name").GetString());
        Assert.Equal(3, data.GetProperty("version").GetInt32());
        Assert.Equal("Điều khoản áp dụng", data.GetProperty("description").GetString());
        var rules = data.GetProperty("rules").EnumerateArray().ToArray();
        Assert.Equal([72, 0], rules.Select(rule => rule.GetProperty("minHoursBefore").GetInt32()));
        Assert.Equal([50m, 0m], rules.Select(rule => rule.GetProperty("refundPercentage").GetDecimal()));
        var reschedule = data.GetProperty("reschedule");
        Assert.Equal(48, reschedule.GetProperty("minHoursBefore").GetInt32());
        Assert.Equal(100_000m, reschedule.GetProperty("fee").GetDecimal());
        Assert.Equal(2, reschedule.GetProperty("maxTimes").GetInt32());
        Assert.Equal(25, data.GetProperty("noShowGraceMinutes").GetInt32());
    }

    [Fact]
    public async Task Get_FallsBackToEffectiveActiveTourPolicy()
    {
        var common = MakePolicy(version: 4);
        await using var factory = new Factory(specific: null, common);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync($"/api/v1/trips/{factory.TripId}/refund-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(common.RefundPolicyId, data.GetProperty("refundPolicyId").GetGuid());
        Assert.Equal(4, data.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Get_ReturnsNotFoundWhenNoPolicyResolves()
    {
        await using var factory = new Factory(specific: null, common: null);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync($"/api/v1/trips/{factory.TripId}/refund-policy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ReturnsConflictAndCodeWhenTheAcceptedVersionIsStale()
    {
        await using var factory = new Factory(specific: MakePolicy(version: 3), common: null);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-Booking", "customer");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "stale-version");

        using var response = await client.PostAsJsonAsync("/api/v1/trip-bookings",
            new { factory.TripId, AcceptedRefundPolicyVersion = 2, ContactPhone = "0901234567" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("REFUND_POLICY_CHANGED", Assert.Single(errors.GetProperty("code").EnumerateArray()).GetString());
        Assert.Contains("3", Assert.Single(errors.GetProperty("acceptedRefundPolicyVersion").EnumerateArray()).GetString());
    }

    private static RefundPolicy MakePolicy(int version) => new()
    {
        RefundPolicyId = Guid.NewGuid(),
        PolicyType = "Tour",
        Status = "Active",
        Name = "Chính sách Tour",
        Version = version,
        Description = "Điều khoản áp dụng",
        RescheduleMinHoursBefore = 48,
        RescheduleFee = 100_000m,
        MaxReschedules = 2,
        NoShowGraceMinutes = 25,
        RefundPolicyRules =
        [
            new() { MinHoursBefore = 0, RefundPercentage = 0 },
            new() { MinHoursBefore = 72, RefundPercentage = 50 },
            new() { MinHoursBefore = 168, RefundPercentage = 100, IsDeleted = true }
        ]
    };

    private sealed class Factory(RefundPolicy? specific, RefundPolicy? common) : WebApplicationFactory<Program>
    {
        public Guid TripId { get; } = Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=unused;Username=unused");
            builder.UseSetting("Jwt:SecretKey", "unit-test-only-signing-key-at-least-32-characters");
            builder.UseSetting("Jwt:Issuer", "rentrek-tests");
            builder.UseSetting("Jwt:Audience", "rentrek-tests");
            builder.UseSetting("RefundDataEncryptionKey", Convert.ToBase64String(new byte[32]));
            builder.UseSetting("Logging:LogLevel:Default", "None");

            var trip = new Trip
            {
                TripId = TripId,
                TourId = Guid.NewGuid(),
                RefundPolicyId = specific?.RefundPolicyId ?? Guid.NewGuid(),
                Status = "Open",
                StartAt = DateTimeOffset.UtcNow.AddDays(1)
            };
            var trips = RepositoryStub.For<ITripRepository>(
                ("GetByIdAsync", args => Task.FromResult((Guid)args[0]! == TripId ? trip : null)));
            var tours = RepositoryStub.For<ITourRepository>(
                ("GetByIdAsync", args => Task.FromResult<Tour?>((Guid)args[0]! == trip.TourId ? new Tour { Status = "Active" } : null)));
            var policies = RepositoryStub.For<IRefundPolicyRepository>(
                ("GetDetailAsync", args => Task.FromResult((Guid)args[0]! == specific?.RefundPolicyId ? specific : null)),
                ("GetCurrentAsync", args => Task.FromResult(args[0] is "Tour" && args[1] is "Active" ? common : null)));
            var bookings = RepositoryStub.For<ITripBookingRepository>(
                ("GetAccountAsync", args => Task.FromResult<Account?>(new Account
                {
                    UserId = (Guid)args[0]!, Email = "buyer@example.test", FullName = "Test Buyer", Status = "Active"
                })),
                ("GetByIdempotencyKeyAsync", _ => Task.FromResult<Order?>(null)),
                ("AcquireIdempotencyLockAsync", _ => Task.CompletedTask),
                ("GetTripForUpdateAsync", args => Task.FromResult((Guid)args[0]! == TripId ? trip : null)));
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "TestBooking";
                        options.DefaultChallengeScheme = "TestBooking";
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestBookingAuthentication>("TestBooking", _ => { });
                services.Replace(ServiceDescriptor.Scoped<IUnitOfWork>(_ => RepositoryStub.For<IUnitOfWork>(
                    ("get_Trips", _ => trips), ("get_Tours", _ => tours),
                    ("get_RefundPolicies", _ => policies), ("get_TripBookings", _ => bookings),
                    ("BeginTransactionAsync", _ => Task.CompletedTask),
                    ("RollbackAsync", _ => Task.CompletedTask))));
            });
        }
    }

    private sealed class TestBookingAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Booking"))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    public class RepositoryStub : DispatchProxy
    {
        private Dictionary<string, Func<object?[], object?>> _handlers = [];

        public static T For<T>(params (string Method, Func<object?[], object?> Handler)[] handlers) where T : class
        {
            var proxy = Create<T, RepositoryStub>();
            ((RepositoryStub)(object)proxy)._handlers = handlers.ToDictionary(entry => entry.Method, entry => entry.Handler);
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == nameof(IDisposable.Dispose) ? null : _handlers[targetMethod!.Name](args ?? []);
    }
}
