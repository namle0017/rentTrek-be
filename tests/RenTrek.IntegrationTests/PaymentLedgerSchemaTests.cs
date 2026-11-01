using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;

namespace RenTrek.IntegrationTests;

public sealed class PaymentLedgerSchemaTests
{
    [Theory]
    [InlineData("PaymentWebhookReceipt", "ProviderEventId")]
    [InlineData("BankTransaction", "ProviderTransactionId")]
    [InlineData("RefundRequest", "IdempotencyKey")]
    [InlineData("PaymentIdempotencyKey", "Key")]
    public void Ledger_HasUniqueIdentity(string entityName, string property)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused;Password=unused").Options);
        var entity = db.Model.FindEntityType($"RenTrek.DAL.Entities.{entityName}");
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Any(p => p.Name == property));
    }

    [Fact]
    public void RefundRequest_PersistsRecipientAndPolicySnapshot()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused;Password=unused").Options);
        var entity = db.Model.FindEntityType("RenTrek.DAL.Entities.RefundRequest");
        Assert.NotNull(entity);
        foreach (var field in new[] { "ProtectedBankDetails", "PolicySnapshot", "AssignedStaffId", "TransferReference", "CompletedAt" })
            Assert.NotNull(entity.FindProperty(field));
    }
}
