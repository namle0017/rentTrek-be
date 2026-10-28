using RenTrek.BLL.DTOs.Promotion.Internal;
using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class PromotionDiscountCalculatorTests
{
    private static PromotionTierOption Tier(
        string id, decimal threshold, string metric = "Amount", string type = "Percent",
        decimal value = 10, bool isGift = false)
        => new()
        {
            TierId = new Guid($"00000000-0000-0000-0000-{id.PadLeft(12, '0')}"),
            MinThreshold = threshold,
            QualificationMetric = metric,
            DiscountType = type,
            DiscountValue = value,
            IsGift = isGift
        };

    private static List<PromotionLineInput> Lines(params decimal[] amounts)
        => amounts.Select((amount, index) => new PromotionLineInput { Index = index, LineAmount = amount }).ToList();

    [Fact]
    public void CalculateOrderDiscount_Percent_FloorsResult()
    {
        Assert.Equal(33_333m, PromotionDiscountCalculator.CalculateOrderDiscount("Percent", 10, 333_333m));
    }

    [Fact]
    public void CalculateOrderDiscount_Percent_IsCappedAtHundred()
    {
        Assert.Equal(300_000m, PromotionDiscountCalculator.CalculateOrderDiscount("percentage", 150, 300_000m));
    }

    [Fact]
    public void CalculateOrderDiscount_Fixed_IsCappedAtBase()
    {
        Assert.Equal(300_000m, PromotionDiscountCalculator.CalculateOrderDiscount("Fixed", 500_000, 300_000m));
    }

    [Theory]
    [InlineData("Unknown", 10, 100_000)]
    [InlineData("Percent", 0, 100_000)]
    [InlineData("Percent", 10, 0)]
    public void CalculateOrderDiscount_InvalidInputs_ReturnZero(string type, decimal value, decimal baseAmount)
    {
        Assert.Equal(0m, PromotionDiscountCalculator.CalculateOrderDiscount(type, value, baseAmount));
    }

    [Fact]
    public void SelectTier_PicksHighestEligibleThreshold()
    {
        var tiers = new List<PromotionTierOption>
        {
            Tier("1", 100_000, value: 5),
            Tier("2", 500_000, value: 10),
            Tier("3", 1_000_000, value: 15)
        };

        var tier = PromotionDiscountCalculator.SelectTier(tiers, 600_000m, 2, 3);

        Assert.Equal(tiers[1].TierId, tier!.TierId);
    }

    [Fact]
    public void SelectTier_NothingEligible_ReturnsNull()
    {
        var tiers = new List<PromotionTierOption> { Tier("1", 500_000) };

        Assert.Null(PromotionDiscountCalculator.SelectTier(tiers, 100_000m, 1, 1));
    }

    [Fact]
    public void SelectTier_SkipsGiftUnsupportedTypeAndUnknownMetric()
    {
        var tiers = new List<PromotionTierOption>
        {
            Tier("1", 0, isGift: true),
            Tier("2", 0, type: "FreeItem"),
            Tier("3", 0, metric: "Weight")
        };

        Assert.Null(PromotionDiscountCalculator.SelectTier(tiers, 1_000_000m, 5, 5));
    }

    [Fact]
    public void SelectTier_UsesQuantityAndDaysMetrics()
    {
        var tiers = new List<PromotionTierOption>
        {
            Tier("1", 3, metric: "Quantity"),
            Tier("2", 5, metric: "BillableDays")
        };

        Assert.Equal(tiers[0].TierId, PromotionDiscountCalculator.SelectTier(tiers, 0m, 3, 2)!.TierId);
        Assert.Equal(tiers[1].TierId, PromotionDiscountCalculator.SelectTier(tiers, 0m, 3, 5)!.TierId);
    }

    [Fact]
    public void CalculateLineDiscounts_Percent_FloorsEachLine()
    {
        var result = PromotionDiscountCalculator.CalculateLineDiscounts("Percent", 10, Lines(333_333m, 100_000m));

        Assert.Equal(2, result.Count);
        Assert.Equal(33_333m, result[0].Amount);
        Assert.Equal(10_000m, result[1].Amount);
    }

    [Fact]
    public void CalculateLineDiscounts_Fixed_SplitSumsExactly()
    {
        var lines = Lines(100_000m, 100_000m, 100_000m);

        var result = PromotionDiscountCalculator.CalculateLineDiscounts("Fixed", 100_000, lines);

        Assert.Equal(100_000m, result.Sum(r => r.Amount));
        Assert.All(result, r => Assert.True(r.Amount == 33_333m || r.Amount == 33_334m));
    }

    [Fact]
    public void CalculateLineDiscounts_Fixed_ProportionalAndCappedAtLineSum()
    {
        var result = PromotionDiscountCalculator.CalculateLineDiscounts("Fixed", 900_000, Lines(100_000m, 300_000m));

        Assert.Equal(400_000m, result.Sum(r => r.Amount));
        Assert.Equal(100_000m, result.Single(r => r.LineIndex == 0).Amount);
        Assert.Equal(300_000m, result.Single(r => r.LineIndex == 1).Amount);
    }

    [Fact]
    public void CalculateLineDiscounts_Fixed_ProportionalShares()
    {
        var result = PromotionDiscountCalculator.CalculateLineDiscounts("Fixed", 100_000, Lines(100_000m, 300_000m));

        Assert.Equal(25_000m, result.Single(r => r.LineIndex == 0).Amount);
        Assert.Equal(75_000m, result.Single(r => r.LineIndex == 1).Amount);
    }

    [Fact]
    public void Calculate_LineScope_DiscountAmountEqualsSumOfLines()
    {
        var lines = Lines(70_000m, 30_001m);

        var result = PromotionDiscountCalculator.Calculate("Fixed", 10_000, true, 100_001m, lines);

        Assert.Equal(10_000m, result.DiscountAmount);
        Assert.Equal(result.DiscountAmount, result.LineDiscounts.Sum(d => d.Amount));
    }

    [Fact]
    public void Calculate_OrderScope_HasNoLineDiscounts()
    {
        var result = PromotionDiscountCalculator.Calculate("Percent", 10, false, 333_333m, Lines(333_333m));

        Assert.Equal(33_333m, result.DiscountAmount);
        Assert.Empty(result.LineDiscounts);
    }
}
