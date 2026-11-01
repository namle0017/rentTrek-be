using Microsoft.AspNetCore.DataProtection;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Identity;

public sealed class PaymentDataProtector(IDataProtectionProvider provider) : IPaymentDataProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("RenTrek.PaymentData.v1");
    public string Protect(string value) => protector.Protect(value);
    public string Unprotect(string value) => protector.Unprotect(value);
}
