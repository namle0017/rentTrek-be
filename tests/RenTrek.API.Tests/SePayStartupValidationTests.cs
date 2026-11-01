using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Options;

namespace RenTrek.BLL.Tests;

public sealed class SePayStartupValidationTests
{
    [Theory]
    [InlineData("SePay:AccountNumber", "")]
    [InlineData("SePay:BankName", "")]
    [InlineData("SePay:Api:Key", "")]
    [InlineData("SePay:AuthMode", "Fallback")]
    public void InvalidCollectionSettings_FailBeforeServingPayment(string field, string value)
    {
        var config = new Dictionary<string, string?> { ["SePay:BankName"] = "MBBank", ["SePay:AccountNumber"] = "0123456789", ["SePay:AccountName"] = "RENTREK", ["SePay:Api:Key"] = "test-only-key", ["SePay:AuthMode"] = "ApiKey" };
        config[field] = value;
        using var services = new ServiceCollection().AddBusinessLayer(new ConfigurationBuilder().AddInMemoryCollection(config).Build()).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<SePaySettings>>().Value);
    }

    [Fact]
    public void ManualRefund_DoesNotRequireProviderRefundCredentials()
    {
        var config = new Dictionary<string, string?> { ["SePay:BankName"] = "MBBank", ["SePay:AccountNumber"] = "0123456789", ["SePay:AccountName"] = "RENTREK", ["SePay:Api:Key"] = "test-only-key" };
        using var services = new ServiceCollection().AddBusinessLayer(new ConfigurationBuilder().AddInMemoryCollection(config).Build()).BuildServiceProvider();
        Assert.Equal("0123456789", services.GetRequiredService<IOptions<SePaySettings>>().Value.AccountNumber);
    }
}
