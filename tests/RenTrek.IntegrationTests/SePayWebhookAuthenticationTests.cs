using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations.Payments;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class SePayWebhookAuthenticationTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(null)] [InlineData("Apikey wrong-key")] [InlineData("Bearer test-only-key")]
    public async Task InvalidApiKey_IsRejectedBeforePersisting(string? auth)
    {
        await using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        var service = WebhookInboxTests.CreateWebhookService(uow, new TestClock());
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.ReceiveAsync(WebhookInboxTests.Payload(WebhookInboxTests.NextEventId(), "NO-CODE", 1), auth));
    }

    [Fact]
    public void Hmac_RejectsTamperingAndStaleTimestampWithoutApiKeyFallback()
    {
        using var db = fixture.CreateDb(); using var uow = new UnitOfWork(db);
        var settings = InitiatePaymentIdempotencyTests.Settings; settings.AuthMode = "Hmac"; settings.WebhookSecret = "test-hmac-secret";
        var service = new SePayWebhookService(uow, new TestClock(), Options.Create(settings), new TestProtector());
        var body = "{\"id\":1}"; var timestamp = TestClock.Now.ToUnixTimeSeconds().ToString();
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.WebhookSecret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
        Assert.True(service.Authenticate(body, null, signature, timestamp));
        Assert.False(service.Authenticate(body + " ", null, signature, timestamp));
        Assert.False(service.Authenticate(body, "Apikey test-only-key", null, timestamp));
        Assert.False(service.Authenticate(body, null, signature, TestClock.Now.AddMinutes(-6).ToUnixTimeSeconds().ToString()));
    }
}
