using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RenTrek.API.BackgroundJobs;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class PaymentContractTests(PostgresFixture fixture)
{
    [Fact]
    public async Task HttpCollection_ProvidesQrAndPollsActualReceivedAmount()
    {
        var seed = await fixture.SeedOrderAsync(); await using var factory = new ApiFactory(fixture);
        using var customer = factory.Client(seed.CustomerId, "Customer"); customer.DefaultRequestHeaders.Add("Idempotency-Key", "http-intent");
        var created = await customer.PostAsJsonAsync($"/api/v1/orders/{seed.OrderId}/payments", new { paymentMethod = "BankTransfer" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1_000_000m, data.GetProperty("amount").GetDecimal());
        var code = data.GetProperty("bankTransfer").GetProperty("transferContent").GetString()!;
        Assert.Matches("^RTP[0-9A-F]{16}$", code);
        using var webhook = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); webhook.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Apikey test-only-key");
        Assert.Equal(HttpStatusCode.OK, (await webhook.PostAsync("/api/v1/payments/webhooks/sepay", new StringContent(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 1_000_000), Encoding.UTF8, "application/json"))).StatusCode);
        var runner = new PaymentMaintenanceRunner(factory.Services.GetRequiredService<IServiceScopeFactory>(), factory.Services.GetRequiredService<TimeProvider>(), NullLogger<PaymentMaintenanceRunner>.Instance);
        await runner.RunOnceAsync();
        var paymentId = data.GetProperty("paymentId").GetGuid();
        var poll = (await customer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{seed.OrderId}/payments/{paymentId}")).GetProperty("data");
        Assert.Equal(1_000_000m, poll.GetProperty("receivedAmount").GetDecimal());
        Assert.Equal(0m, poll.GetProperty("remainingAmount").GetDecimal());
        Assert.Equal("Paid", poll.GetProperty("orderPaymentStatus").GetString());
        var replay = await customer.PostAsJsonAsync($"/api/v1/orders/{seed.OrderId}/payments", new { paymentMethod = "BankTransfer" });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("bankTransfer").GetProperty("qrImageUrl").ValueKind);
    }

    [Fact]
    public async Task RefundSubmit_ReplayReturns200AndSameId()
    {
        var seed = await new CustomerRefundRequestTests(fixture).PaidOrder(); await using var factory = new ApiFactory(fixture);
        using var customer = factory.Client(seed.CustomerId, "Customer"); customer.DefaultRequestHeaders.Add("Idempotency-Key", "replay-refund");
        var path = $"/api/v1/orders/{seed.OrderId}/refund-requests";
        var first = await customer.PostAsJsonAsync(path, CustomerRefundRequestTests.Request());
        var second = await customer.PostAsJsonAsync(path, CustomerRefundRequestTests.Request());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid(), (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task RentalDetail_ShowsActualPartialMoneyBeforeAndAfterExpiry()
    {
        var seed = await fixture.SeedOrderAsync(400_000); var hooks = new WebhookInboxTests(fixture); var code = await hooks.Intent(seed);
        await hooks.Send(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), code, 100_000));
        await using var factory = new ApiFactory(fixture); using var customer = factory.Client(seed.CustomerId, "Customer");
        var detail = (await customer.GetFromJsonAsync<JsonElement>($"/api/v1/rental-orders/{seed.OrderId}")).GetProperty("data");
        Assert.Equal(100_000m, detail.GetProperty("amounts").GetProperty("paidAmount").GetDecimal());
        await using (var db = fixture.CreateDb()) { using var uow = new RenTrek.DAL.UnitOfWorks.UnitOfWork(db); await new RenTrek.BLL.Services.Implementations.Payments.OrderPaymentLifecycleService(uow, new TestClock { Current = TestClock.Now.AddMinutes(15) }).ExpireAsync(seed.OrderId); }
        var expired = (await customer.GetFromJsonAsync<JsonElement>($"/api/v1/rental-orders/{seed.OrderId}")).GetProperty("data");
        Assert.Equal(100_000m, expired.GetProperty("amounts").GetProperty("paidAmount").GetDecimal());
        Assert.DoesNotContain(expired.GetProperty("timeline").EnumerateArray(), entry => entry.GetProperty("event").GetString() == "Paid");
    }

    [Fact]
    public async Task RentalDetail_DoesNotOfferPayForNullHold()
    {
        var seed = await fixture.SeedOrderAsync();
        await using (var db = fixture.CreateDb()) { (await db.Orders.FindAsync(seed.OrderId))!.HoldExpiresAt = null; await db.SaveChangesAsync(); }
        await using var factory = new ApiFactory(fixture); using var customer = factory.Client(seed.CustomerId, "Customer");
        var detail = (await customer.GetFromJsonAsync<JsonElement>($"/api/v1/rental-orders/{seed.OrderId}")).GetProperty("data");
        Assert.False(detail.GetProperty("allowedActions").GetProperty("canPay").GetBoolean());
    }

    [Fact]
    public async Task Readiness_RequiresPaymentSchemaAndProtection()
    {
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
