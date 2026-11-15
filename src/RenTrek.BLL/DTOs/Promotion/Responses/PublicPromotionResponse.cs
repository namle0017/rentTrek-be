namespace RenTrek.BLL.DTOs.Promotion.Responses;

/// <summary>Mã khuyến mãi đang chạy cho đơn thuê, hiển thị để khách chọn ở giỏ hàng.</summary>
public sealed record PublicPromotionResponse(
    string Code,
    string Name,
    string? Description,
    DateTime EndAt,
    IReadOnlyList<PublicPromotionTierResponse> Tiers);
