using RenTrek.BLL.DTOs.Rental.Internal;
using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class RentalPricingCalculatorTests
{
    private static readonly Guid Tier1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Tier2 = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Tier3 = new("00000000-0000-0000-0000-000000000003");

    private static RentalPriceTierOption Tier(Guid id, int min, int? max, decimal price)
        => new() { PriceTierId = id, MinDays = min, MaxDays = max, DailyPrice = price };

    private static List<RentalPriceTierOption> StandardTiers() =>
    [
        Tier(Tier1, 1, 2, 100_000),
        Tier(Tier2, 3, 6, 80_000),
        Tier(Tier3, 7, null, 60_000)
    ];

    [Theory]
    [InlineData(1, 100_000)]
    [InlineData(2, 100_000)]
    [InlineData(3, 80_000)]
    [InlineData(6, 80_000)]
    [InlineData(7, 60_000)]
    [InlineData(10, 60_000)]
    public void SelectTier_PicksMatchingTier(int days, int expectedPrice)
    {
        var tier = RentalPricingCalculator.SelectTier(StandardTiers(), days);

        Assert.NotNull(tier);
        Assert.Equal(expectedPrice, tier!.DailyPrice);
    }

    [Fact]
    public void SelectTier_OverlappingTiers_PicksHighestMinDays()
    {
        var tiers = new List<RentalPriceTierOption>
        {
            Tier(Tier1, 1, null, 100_000),
            Tier(Tier2, 3, null, 70_000)
        };

        var tier = RentalPricingCalculator.SelectTier(tiers, 5);

        Assert.Equal(Tier2, tier!.PriceTierId);
    }

    [Fact]
    public void SelectTier_SameMinDays_PicksLowestId()
    {
        var tiers = new List<RentalPriceTierOption>
        {
            Tier(Tier2, 1, null, 90_000),
            Tier(Tier1, 1, null, 95_000)
        };

        var tier = RentalPricingCalculator.SelectTier(tiers, 2);

        Assert.Equal(Tier1, tier!.PriceTierId);
    }

    [Fact]
    public void SelectTier_NoMatchingTier_ReturnsNull()
    {
        var tiers = new List<RentalPriceTierOption> { Tier(Tier2, 3, 6, 80_000) };

        Assert.Null(RentalPricingCalculator.SelectTier(tiers, 2));
        Assert.Null(RentalPricingCalculator.SelectTier(tiers, 7));
        Assert.Null(RentalPricingCalculator.SelectTier([], 1));
    }

    [Fact]
    public void CalculateLineAmount_MultipliesQuantityDaysAndPrice()
    {
        Assert.Equal(480_000m, RentalPricingCalculator.CalculateLineAmount(2, 3, 80_000m));
    }
}
