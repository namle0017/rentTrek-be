using RenTrek.BLL.Constants;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Helpers.Implementations;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class TripStatusTimeRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddDays(2);

    [Fact]
    public void InProgress_RequiresClosedForBookingAndStartTime()
    {
        Assert.NotNull(TripStatusTimeRules.Validate(TripStatuses.Open, TripStatuses.InProgress, Start, End, Start));
        Assert.NotNull(TripStatusTimeRules.Validate(TripStatuses.ClosedForBooking, TripStatuses.InProgress, Start, End, Start.AddMinutes(-1)));
        Assert.Null(TripStatusTimeRules.Validate(TripStatuses.ClosedForBooking, TripStatuses.InProgress, Start, End, Start));
    }

    [Fact]
    public void Completed_RequiresInProgressAndEndTime()
    {
        Assert.NotNull(TripStatusTimeRules.Validate(TripStatuses.ClosedForBooking, TripStatuses.Completed, Start, End, End));
        Assert.NotNull(TripStatusTimeRules.Validate(TripStatuses.InProgress, TripStatuses.Completed, Start, End, End.AddMinutes(-1)));
        Assert.Null(TripStatusTimeRules.Validate(TripStatuses.InProgress, TripStatuses.Completed, Start, End, End));
    }

    [Fact]
    public void Cancelled_IsBlockedOnceTripStarted()
    {
        Assert.Null(TripStatusTimeRules.Validate(TripStatuses.Open, TripStatuses.Cancelled, Start, End, Start));
        Assert.NotNull(TripStatusTimeRules.Validate(TripStatuses.InProgress, TripStatuses.Cancelled, Start, End, Start));
        Assert.NotNull(TripStatusTimeRules.Validate(TripStatuses.Completed, TripStatuses.Cancelled, Start, End, End));
    }
}

public sealed class SimulatedTimeProviderTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void SetUtcNow_ShiftsAndKeepsTicking_ResetReturnsToRealTime()
    {
        var real = new FixedClock(new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero));
        var clock = new SimulatedTimeProvider(real);
        var target = new DateTimeOffset(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

        clock.SetUtcNow(target);
        Assert.Equal(target, clock.GetUtcNow());

        real.Now = real.Now.AddMinutes(30);
        Assert.Equal(target.AddMinutes(30), clock.GetUtcNow());

        clock.Reset();
        Assert.Equal(real.Now, clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.Offset);
    }
}
