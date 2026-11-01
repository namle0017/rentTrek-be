using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Rental.Internal;
using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class RentalAvailabilityCalculatorTests
{
    private static readonly Guid V1 = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid V2 = new("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid BundleId = new("00000000-0000-0000-0000-0000000000b1");

    private static RentalLineComponentItem Component(Guid variantId, int perUnit, int lineQuantity)
        => new()
        {
            ProductVariantId = variantId,
            VariantName = variantId == V1 ? "Lều" : "Túi ngủ",
            QuantityPerUnit = perUnit,
            TotalQuantity = perUnit * lineQuantity
        };

    private static ResolvedRentalLineResult VariantLine(int index, Guid variantId, int quantity)
        => new()
        {
            Index = index,
            ItemType = RentalItemTypes.Variant,
            ProductVariantId = variantId,
            Quantity = quantity,
            Components = [Component(variantId, 1, quantity)]
        };

    private static ResolvedRentalLineResult BundleLine(int index, int quantity)
        => new()
        {
            Index = index,
            ItemType = RentalItemTypes.Bundle,
            BundleId = BundleId,
            Quantity = quantity,
            Components = [Component(V1, 1, quantity), Component(V2, 2, quantity)]
        };

    [Fact]
    public void AggregateDemand_SumsVariantAndBundleComponents()
    {
        var lines = new List<ResolvedRentalLineResult> { VariantLine(0, V1, 2), BundleLine(1, 2) };

        var demand = RentalAvailabilityCalculator.AggregateDemand(lines);

        Assert.Equal(4, demand[V1]);
        Assert.Equal(4, demand[V2]);
    }

    [Fact]
    public void BundleAvailability_ReturnsMinimumFitAndLimitingVariant()
    {
        var components = new List<RentalLineComponentItem> { Component(V1, 1, 1), Component(V2, 2, 1) };
        var available = new Dictionary<Guid, int> { [V1] = 5, [V2] = 3 };

        var (quantity, limiting) = RentalAvailabilityCalculator.BundleAvailability(components, available);

        Assert.Equal(1, quantity);
        Assert.Equal(V2, limiting);
    }

    [Fact]
    public void BundleAvailability_NoComponents_ReturnsZeroWithoutLimiting()
    {
        var (quantity, limiting) = RentalAvailabilityCalculator.BundleAvailability([], new Dictionary<Guid, int>());

        Assert.Equal(0, quantity);
        Assert.Null(limiting);
    }

    [Fact]
    public void BuildLineResults_SharedVariantAcrossLines_ReportsAggregatedShortage()
    {
        var lines = new List<ResolvedRentalLineResult> { VariantLine(0, V1, 2), BundleLine(1, 2) };
        var demand = RentalAvailabilityCalculator.AggregateDemand(lines);
        var available = new Dictionary<Guid, int> { [V1] = 3, [V2] = 10 };

        var results = RentalAvailabilityCalculator.BuildLineResults(lines, demand, available);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsAvailable);
        Assert.Equal(3, results[0].AvailableQuantity);
        var shortage = Assert.Single(results[0].Shortages);
        Assert.Equal(V1, shortage.ProductVariantId);
        Assert.Equal(4, shortage.Requested);
        Assert.Equal(3, shortage.Available);

        Assert.False(results[1].IsAvailable);
        Assert.Equal(3, results[1].AvailableQuantity);
        Assert.Equal(V1, results[1].LimitingComponent!.ProductVariantId);
    }

    [Fact]
    public void BuildLineResults_EnoughStock_AllAvailable()
    {
        var lines = new List<ResolvedRentalLineResult> { VariantLine(0, V1, 2) };
        var demand = RentalAvailabilityCalculator.AggregateDemand(lines);
        var available = new Dictionary<Guid, int> { [V1] = 5 };

        var results = RentalAvailabilityCalculator.BuildLineResults(lines, demand, available);

        var result = Assert.Single(results);
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Shortages);
        Assert.Equal(5, result.AvailableQuantity);
        Assert.Null(result.LimitingComponent);
    }
}
