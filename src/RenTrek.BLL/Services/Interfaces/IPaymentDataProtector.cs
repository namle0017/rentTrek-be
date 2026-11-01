namespace RenTrek.BLL.Services.Interfaces;

public interface IPaymentDataProtector
{
    string Protect(string value);
    string Unprotect(string value);
}
