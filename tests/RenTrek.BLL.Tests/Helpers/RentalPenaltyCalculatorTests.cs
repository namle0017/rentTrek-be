using RenTrek.BLL.Constants;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Implementations;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class RentalPenaltyCalculatorTests
{
    private static readonly RentalPolicySnapshot Policy = RentalPolicyService.Default;
    private static readonly DateTime Start = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddDays(5);
    private const decimal Deposit = 200_000m;
    private const decimal Paid = 1_200_000m;

    [Theory]
    [InlineData(1_000_000, 20, 200_000)]
    [InlineData(1_234_567, 20, 247_000)] // 246.913,4 → làm tròn lên 1.000
    [InlineData(0, 20, 0)]
    [InlineData(500_000, 0, 0)]
    public void CalculateDeposit_RoundsUpToThousand(decimal subtotal, decimal percent, decimal expected)
        => Assert.Equal(expected, RentalPenaltyCalculator.CalculateDeposit(subtotal, percent));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(23.99, 0)]
    [InlineData(24, 1)]
    [InlineData(47.9, 1)]
    [InlineData(48, 2)]
    [InlineData(72, 3)]
    public void FullDaysLate_CountsCompleted24hBlocks(double hours, int expected)
        => Assert.Equal(expected, RentalPenaltyCalculator.FullDaysLate(Start, Start.AddHours(hours)));

    [Theory]
    [InlineData(10, 0, false)]     // chưa đủ 1 ngày: miễn phí
    [InlineData(24, 10_000, false)] // 1 ngày: 5% cọc
    [InlineData(48, 40_000, false)] // 2 ngày: 20% cọc
    [InlineData(72, 200_000, true)] // 3 ngày: no-show mất trọn cọc
    public void GetPickupStatus_AppliesTiersOnDeposit(double hoursLate, decimal expectedFee, bool expectedNoShow)
    {
        var status = RentalPenaltyCalculator.GetPickupStatus(Policy, Deposit, Start, End, Start.AddHours(hoursLate));
        Assert.Equal(expectedFee, status.Fee);
        Assert.Equal(expectedNoShow, status.IsNoShow);
    }

    [Fact]
    public void GetPickupStatus_ShortRental_IsNoShowOnceRentalEnds()
    {
        // Đơn 1 ngày: không đến trong cả thời gian thuê vẫn mất trọn cọc dù chưa tới mốc 3 ngày
        var end = Start.AddDays(1);
        var status = RentalPenaltyCalculator.GetPickupStatus(Policy, Deposit, Start, end, end);
        Assert.True(status.IsNoShow);
        Assert.Equal(Deposit, status.Fee);
    }

    [Fact]
    public void QuoteCancellation_BeforePickupDay_IsFreeFullRefund()
    {
        var quote = RentalPenaltyCalculator.QuoteCancellation(Policy, Paid, Deposit, Start, End, Start.AddDays(-2), 0);
        Assert.True(quote.Allowed);
        Assert.Equal(RentalCancellationTypes.FreeCancel, quote.CancellationType);
        Assert.Equal(0m, quote.Fee);
        Assert.Equal(Paid, quote.RefundAmount);
    }

    [Fact]
    public void QuoteCancellation_OnPickupDayWithin24h_IsStillFree()
    {
        var quote = RentalPenaltyCalculator.QuoteCancellation(Policy, Paid, Deposit, Start, End, Start.AddHours(20), 0);
        Assert.Equal(RentalCancellationTypes.FreeCancel, quote.CancellationType);
        Assert.Equal(Paid, quote.RefundAmount);
    }

    [Fact]
    public void QuoteCancellation_ThirdFreeCancelIn30Days_Costs5PercentOfDeposit()
    {
        var quote = RentalPenaltyCalculator.QuoteCancellation(Policy, Paid, Deposit, Start, End, Start.AddDays(-2), freeCancellationsInWindow: 2);
        Assert.Equal(RentalCancellationTypes.OverLimitCancel, quote.CancellationType);
        Assert.Equal(10_000m, quote.Fee);
        Assert.Equal(Paid - 10_000m, quote.RefundAmount);
    }

    [Fact]
    public void QuoteCancellation_LateTwoDays_Costs20PercentOfDeposit()
    {
        var quote = RentalPenaltyCalculator.QuoteCancellation(Policy, Paid, Deposit, Start, End, Start.AddHours(50), 0);
        Assert.Equal(RentalCancellationTypes.LateCancel, quote.CancellationType);
        Assert.Equal(40_000m, quote.Fee);
        Assert.Equal(Paid - 40_000m, quote.RefundAmount);
    }

    [Fact]
    public void QuoteCancellation_AtNoShow_IsNotAllowed()
    {
        var quote = RentalPenaltyCalculator.QuoteCancellation(Policy, Paid, Deposit, Start, End, Start.AddDays(3), 0);
        Assert.False(quote.Allowed);
        Assert.Equal(RentalCancellationTypes.NoShow, quote.CancellationType);
        Assert.Equal(Paid - Deposit, quote.RefundAmount);
    }

    [Theory]
    [InlineData(30, 0, false)]       // trễ 1 ngày: chỉ tính tiền thuê ngày (ngoài calculator này)
    [InlineData(72, 10_000, false)]  // 3 ngày: 5% cọc
    [InlineData(96, 40_000, false)]  // 4 ngày: 20% cọc
    [InlineData(120, 200_000, false)] // 5 ngày: mất cọc
    [InlineData(168, 200_000, true)] // 7 ngày: nghi mất
    public void GetReturnStatus_TiersShiftedByTwoDays(double hoursLate, decimal expectedFee, bool suspectedLoss)
    {
        var status = RentalPenaltyCalculator.GetReturnStatus(Policy, Deposit, End, End.AddHours(hoursLate));
        Assert.Equal(expectedFee, status.PenaltyFee);
        Assert.Equal(suspectedLoss, status.IsSuspectedLoss);
    }

    [Theory]
    [InlineData(-10, true)]
    [InlineData(47, true)]
    [InlineData(71, true)]  // vẫn trong ngày trễ thứ 2 (chưa đủ 72h)
    [InlineData(72, false)] // tới bậc phạt đầu tiên thì không gia hạn được
    public void CanExtend_UntilEndOfSecondLateDay(double hoursAfterEnd, bool expected)
        => Assert.Equal(expected, RentalPenaltyCalculator.CanExtend(Policy, End, End.AddHours(hoursAfterEnd)));
}
