namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminRentalComponentResponse(Guid ProductVariantId, string Name, int QuantityPerBundle);
