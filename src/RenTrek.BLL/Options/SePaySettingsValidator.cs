using Microsoft.Extensions.Options;

namespace RenTrek.BLL.Options;

public sealed class SePaySettingsValidator : IValidateOptions<SePaySettings>
{
    public ValidateOptionsResult Validate(string? name, SePaySettings value)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(value.BankName)) errors.Add("SePay:BankName is required.");
        if (string.IsNullOrWhiteSpace(value.AccountNumber)) errors.Add("SePay:AccountNumber is required.");
        if (string.IsNullOrWhiteSpace(value.AccountName)) errors.Add("SePay:AccountName is required.");
        if (value.AuthMode is not ("ApiKey" or "Hmac")) errors.Add("SePay:AuthMode must be ApiKey or Hmac.");
        if (value.AuthMode == "ApiKey" && string.IsNullOrWhiteSpace(value.Api.Key)) errors.Add("SePay:Api:Key is required.");
        if (value.AuthMode == "Hmac" && string.IsNullOrWhiteSpace(value.WebhookSecret)) errors.Add("SePay:WebhookSecret is required.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
