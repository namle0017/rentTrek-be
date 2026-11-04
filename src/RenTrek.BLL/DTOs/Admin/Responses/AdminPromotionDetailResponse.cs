namespace RenTrek.BLL.DTOs.Admin.Responses;

public sealed record AdminPromotionDetailResponse(AdminPromotionResponse Promotion, List<AdminPromotionTierResponse> Tiers);
