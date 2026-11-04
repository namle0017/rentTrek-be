namespace RenTrek.BLL.DTOs.Admin.Requests;

public sealed record PromotionTierRequest(string QualificationMetric, decimal MinThreshold, string DiscountType, decimal DiscountValue, Guid? GiftProductVariantId, int? GiftQuantity);
