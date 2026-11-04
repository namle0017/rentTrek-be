namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminRentalResponse(Guid OrderId, string OrderCode, AdminCustomerResponse Customer, AdminRentalPeriodResponse RentalPeriod, int TotalItemsCount, int IssuedItemsCount, int ReturnedItemsCount, decimal TotalAmount, string? HandlerFullName, string Status, string PaymentStatus, bool IsOverdue);
