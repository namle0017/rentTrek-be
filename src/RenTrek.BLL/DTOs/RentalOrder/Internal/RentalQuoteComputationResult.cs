using RenTrek.BLL.DTOs.Promotion.Internal;
using RenTrek.BLL.DTOs.Rental.Internal;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.DTOs.RentalOrder.Internal;

/// <summary>
/// Kết quả tính toán nội bộ cho báo giá và tạo đơn thuê (dùng chung một lần tính để hai luồng luôn khớp nhau).
/// </summary>
internal class RentalQuoteComputationResult
{
    public RentalPeriodResult Period { get; set; } = new();

    public List<ResolvedRentalLineResult> ResolvedLines { get; set; } = [];

    public List<PricedRentalLineResult> PricedLines { get; set; } = [];

    public List<RentalLineAvailabilityResult> Availability { get; set; } = [];

    public decimal SubtotalAmount { get; set; }

    public PromotionEvaluationResult Promotion { get; set; } = new();

    /// <summary>Mức giảm thực tế sau khi chặn không vượt quá tạm tính; 0 khi mã không áp được.</summary>
    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal TotalPayable { get; set; }

    /// <summary>Chính sách hoàn tiền thuê hiện hành; null nếu chưa cấu hình.</summary>
    public RefundPolicy? RefundPolicy { get; set; }

    public int HoldMinutes { get; set; }

    /// <summary>Chính sách thuê đồ (cọc, phạt) áp cho đơn.</summary>
    public RenTrek.BLL.Helpers.RentalPolicySnapshot RentalPolicy { get; set; } = RenTrek.BLL.Services.Implementations.RentalPolicyService.Default;

    /// <summary>Giá trị thiết bị / combo theo chỉ số dòng (0 nếu chưa nhập).</summary>
    public Dictionary<int, decimal> UnitReplacementValues { get; set; } = [];

    public bool IsAllAvailable => Availability.All(line => line.IsAvailable);
}
