namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminPromotionUsageResponse(Guid AppliedPromotionId, Guid OrderId, string OrderCode, decimal DiscountAmount, int? GiftQuantity, string Status, DateTime AppliedAt);
