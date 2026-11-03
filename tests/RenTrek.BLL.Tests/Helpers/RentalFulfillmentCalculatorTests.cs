using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class RentalFulfillmentCalculatorTests
{
    private static readonly DateTime End = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-5, 0)]    // trả sớm
    [InlineData(0, 0)]     // đúng hạn
    [InlineData(2, 0)]     // trong ân hạn 2 giờ
    [InlineData(3, 1)]     // quá ân hạn → 1 ngày
    [InlineData(24, 1)]
    [InlineData(25, 2)]    // sang 24 giờ thứ hai
    public void CalculateLateDays_UsesGraceThenStartedDays(int hoursAfterEnd, int expectedDays)
    {
        Assert.Equal(expectedDays, RentalFulfillmentCalculator.CalculateLateDays(End, End.AddHours(hoursAfterEnd), 2m));
    }

    [Theory]
    [InlineData(1, 2, 2, 1)]  // lẻ: 2 lều, trễ 1 → 1 đơn vị
    [InlineData(1, 1, 2, 1)]  // combo 2 món × 1, trễ 1 món → vẫn 1 combo
    [InlineData(3, 2, 4, 2)]  // combo 2 món × 2, trễ 3 món → 2 combo
    [InlineData(0, 2, 2, 0)]
    public void CalculateLateUnits_RoundsUpToRentalUnits(int lateAssets, int quantity, int assets, int expected)
    {
        Assert.Equal(expected, RentalFulfillmentCalculator.CalculateLateUnits(lateAssets, quantity, assets));
    }

    [Fact]
    public void CalculateLateFee_MultipliesDaysPriceAndUnits()
    {
        Assert.Equal(160_000m, RentalFulfillmentCalculator.CalculateLateFee(2, 80_000m, 1));
        Assert.Equal(0m, RentalFulfillmentCalculator.CalculateLateFee(0, 80_000m, 3));
    }

    [Fact]
    public void Settle_OffsetsChargesFromDepositFirst()
    {
        // PRD §8.7: cọc 300.000, phí 120.000 → hoàn 180.000, không thu thêm (UAT-20)
        Assert.Equal((180_000m, 0m), RentalFulfillmentCalculator.Settle(300_000m, 120_000m));
        Assert.Equal((0m, 50_000m), RentalFulfillmentCalculator.Settle(100_000m, 150_000m));
        Assert.Equal((0m, 120_000m), RentalFulfillmentCalculator.Settle(0m, 120_000m));
    }
}
