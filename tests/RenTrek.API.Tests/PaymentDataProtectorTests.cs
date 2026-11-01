using Microsoft.AspNetCore.DataProtection;
using RenTrek.API.Identity;

namespace RenTrek.API.Tests;

public sealed class PaymentDataProtectorTests
{
    [Fact]
    public void ProtectedRecipient_CanBeReadAfterProviderRestartWithSameKeyRing()
    {
        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "rentrek-test-keyring-" + Guid.NewGuid().ToString("N")));
        var first = new PaymentDataProtector(DataProtectionProvider.Create(directory, config => config.SetApplicationName("RenTrek.Payments")));
        const string recipient = "{\"AccountNumber\":\"0012345678\"}";
        var encrypted = first.Protect(recipient); Assert.DoesNotContain("0012345678", encrypted);
        var restarted = new PaymentDataProtector(DataProtectionProvider.Create(directory, config => config.SetApplicationName("RenTrek.Payments")));
        Assert.Equal(recipient, restarted.Unprotect(encrypted));
    }
}
