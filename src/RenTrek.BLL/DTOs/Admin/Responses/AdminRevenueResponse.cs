namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminRevenueResponse(DateTime Period, string OrderType, decimal Collections, decimal Refunds, decimal NetRevenueVnd);
