using RenTrek.BLL.DTOs.Promotion;
using RenTrek.BLL.DTOs.Rental;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.DTOs.RentalOrder;

/// <summary>
/// Kết quả tính toán nội bộ cho báo giá và tạo đơn thuê (dùng chung một lần tính để hai luồng luôn khớp nhau).
/// </summary>
internal class RentalQuoteComputation
{
    public RentalPeriod Period { get; set; } = new();

    public List<ResolvedRentalLine> ResolvedLines { get; set; } = [];

    public List<PricedRentalLine> PricedLines { get; set; } = [];

    public List<RentalLineAvailability> Availability { get; set; } = [];

    public decimal SubtotalAmount { get; set; }

    public EvaluatePromotionResponse Promotion { get; set; } = new();

    /// <summary>Mức giảm thực tế sau khi chặn không vượt quá tạm tính; 0 khi mã không áp được.</summary>
    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal TotalPayable { get; set; }

    /// <summary>Chính sách hoàn tiền thuê hiện hành; null nếu chưa cấu hình.</summary>
    public RefundPolicy? RefundPolicy { get; set; }

    public int HoldMinutes { get; set; }

    public bool IsAllAvailable => Availability.All(line => line.IsAvailable);
}
