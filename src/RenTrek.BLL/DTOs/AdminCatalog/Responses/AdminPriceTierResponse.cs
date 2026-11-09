namespace RenTrek.BLL.DTOs.AdminCatalog.Responses;

public sealed record AdminPriceTierResponse(int MinDays, int? MaxDays, decimal DailyPrice);
