namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminOrderResponse(Guid OrderId, string OrderCode, string OrderType, AdminCustomerResponse Customer, string ServiceSummary, decimal TotalAmount, string Status, string PaymentStatus, DateTimeOffset? HoldExpiresAt, DateTimeOffset? ReconciliationDeadlineAt, bool HasPendingRefund, DateTimeOffset CreatedAt);
