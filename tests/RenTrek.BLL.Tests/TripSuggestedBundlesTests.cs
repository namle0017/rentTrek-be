using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class TripSuggestedBundlesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid VariantId = Guid.NewGuid();

    [Fact]
    public async Task DraftTrip_IsNotFound()
    {
        var service = Service(Trip(TripStatuses.Draft, Now.AddDays(5)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetSuggestedForTripAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task StartedTrip_ReturnsProvidedItemsButNoBundles()
    {
        var result = await Service(Trip(TripStatuses.InProgress, Now.AddHours(-1))).GetSuggestedForTripAsync(Guid.NewGuid());

        Assert.Empty(result.Bundles);
        Assert.Equal([VariantId], result.ProvidedVariantIds);
    }

    [Fact]
    public async Task FutureTrip_SkipsBundlesNoLongerSold_AndUsesTripDates()
    {
        var trip = Trip(TripStatuses.Open, Now.AddDays(5));
        var result = await Service(trip).GetSuggestedForTripAsync(Guid.NewGuid());

        Assert.Empty(result.Bundles);
        Assert.Equal(trip.StartAt.UtcDateTime, result.RentalFrom);
        Assert.Equal(trip.EndAt.UtcDateTime, result.RentalTo);
    }

    private static Trip Trip(string status, DateTimeOffset startAt)
        => new() { TripId = Guid.NewGuid(), TourId = Guid.NewGuid(), Status = status, StartAt = startAt, EndAt = startAt.AddDays(2) };

    private static ProductBundleService Service(Trip trip)
    {
        var tour = new Tour
        {
            SuggestedBundles = [new TourSuggestedBundle { BundleId = Guid.NewGuid(), Note = "Đủ cho 2 người" }],
            ProvidedItems = [new TourProvidedItem { ProductVariantId = VariantId }, new TourProvidedItem()]
        };
        var unitOfWork = TestProxy.Create<IUnitOfWork>(
            ("get_Trips", _ => TestProxy.Create<ITripRepository>(("GetByIdAsync", _ => Task.FromResult<Trip?>(trip)))),
            ("get_Tours", _ => TestProxy.Create<ITourRepository>(("GetSuggestionsAsync", _ => Task.FromResult<Tour?>(tour)))),
            // Combo đã ngừng bán: chi tiết trả null.
            ("get_ProductBundles", _ => TestProxy.Create<IProductBundleRepository>(
                ("GetActiveDetailAsync", _ => Task.FromResult<ProductBundleDetailRow?>(null)))));
        return new ProductBundleService(unitOfWork, null!, new FixedTime());
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
