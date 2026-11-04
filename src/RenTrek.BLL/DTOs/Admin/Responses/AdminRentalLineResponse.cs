namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminRentalLineResponse(Guid RentalOrderItemId, string Name, int Quantity, int BillableDays, decimal DailyPrice, decimal LineAmount, List<AdminRentalComponentResponse> Components, List<AdminAssetResponse> Assets);
