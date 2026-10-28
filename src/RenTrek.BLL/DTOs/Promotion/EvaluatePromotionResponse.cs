using RenTrek.BLL.DTOs.Rental;

namespace RenTrek.BLL.DTOs.Promotion;

/// <summary>
/// Kết quả đánh giá khuyến mãi. Lỗi nghiệp vụ được trả qua <see cref="ErrorCode"/> / <see cref="Message"/>, không ném exception.
/// </summary>
public class EvaluatePromotionResponse
{
    /// <summary>True khi mã hợp lệ và tạo ra mức giảm lớn hơn 0.</summary>
    public bool IsApplied { get; set; }

    public Guid? PromotionId { get; set; }

    /// <summary>Bậc khuyến mãi được chọn.</summary>
    public Guid? TierId { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>True khi khuyến mãi áp dụng theo từng dòng (xem <see cref="LineDiscounts"/>).</summary>
    public bool IsLineScope { get; set; }

    /// <summary>Mức giảm theo từng dòng (chỉ có khi <see cref="IsLineScope"/>).</summary>
    public List<PromotionLineDiscount> LineDiscounts { get; set; } = [];

    /// <summary>Mã lỗi nghiệp vụ (PROMOTION_INVALID / PROMOTION_EXHAUSTED); null khi không có lỗi.</summary>
    public string? ErrorCode { get; set; }

    public string? Message { get; set; }
}
