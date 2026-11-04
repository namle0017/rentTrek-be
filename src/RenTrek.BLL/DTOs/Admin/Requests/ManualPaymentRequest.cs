namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record ManualPaymentRequest(decimal Amount, string PaymentMethod, string ManualReference, string Reason);
