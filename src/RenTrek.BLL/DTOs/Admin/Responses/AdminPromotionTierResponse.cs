namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminPromotionTierResponse(Guid PromotionTierId, string QualificationMetric, decimal MinThreshold, string DiscountType, decimal DiscountValue, Guid? GiftProductVariantId, int? GiftQuantity);
