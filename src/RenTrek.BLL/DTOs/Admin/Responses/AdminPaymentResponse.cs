namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminPaymentResponse(Guid PaymentId, string PaymentType, string Purpose, decimal Amount, string PaymentMethod, string? ProviderTransactionId, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ProcessedAt);
