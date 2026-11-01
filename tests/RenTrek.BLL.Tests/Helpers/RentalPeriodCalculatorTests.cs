using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class RentalPeriodCalculatorTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Start.AddDays(-1);

    [Theory]
    [InlineData(24 * 60, 1)]
    [InlineData(24 * 60 + 1, 2)]
    [InlineData(3 * 24 * 60, 3)]
    [InlineData(60, 1)]
    [InlineData(1, 1)]
    [InlineData(48 * 60 + 1, 3)]
    public void CalculateBillableDays_RoundsUpToWholeDays(int minutes, int expected)
    {
        Assert.Equal(expected, RentalPeriodCalculator.CalculateBillableDays(Start, Start.AddMinutes(minutes)));
    }

    [Fact]
    public void CalculateBillableDays_NonPositiveSpan_ReturnsMinimumOne()
    {
        Assert.Equal(1, RentalPeriodCalculator.CalculateBillableDays(Start, Start));
        Assert.Equal(1, RentalPeriodCalculator.CalculateBillableDays(Start, Start.AddHours(-5)));
    }

    [Fact]
    public void Validate_ValidPeriod_ReturnsUtcPeriodWithDays()
    {
        var period = RentalPeriodCalculator.Validate(Start, Start.AddHours(25), Now);

        Assert.Equal(2, period.BillableDays);
        Assert.Equal(DateTimeKind.Utc, period.StartUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, period.EndUtc.Kind);
    }

    [Fact]
    public void Validate_UnspecifiedKind_IsTreatedAsUtc()
    {
        var start = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);

        var period = RentalPeriodCalculator.Validate(start, start.AddDays(1), Now);

        Assert.Equal(Start, period.StartUtc);
        Assert.Equal(DateTimeKind.Utc, period.StartUtc.Kind);
    }

    [Fact]
    public void Validate_EndNotAfterStart_ThrowsWithErrorCode()
    {
        var ex = Assert.Throws<BadRequestException>(() => RentalPeriodCalculator.Validate(Start, Start, Now));

        Assert.Equal(ErrorCodes.InvalidRentalPeriod, ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_StartMoreThanFiveMinutesInPast_ThrowsWithErrorCode()
    {
        var ex = Assert.Throws<BadRequestException>(
            () => RentalPeriodCalculator.Validate(Now.AddMinutes(-6), Now.AddDays(1), Now));

        Assert.Equal(ErrorCodes.InvalidRentalPeriod, ex.ErrorCode);
    }

    [Fact]
    public void Validate_StartWithinFiveMinuteSkew_IsAccepted()
    {
        var period = RentalPeriodCalculator.Validate(Now.AddMinutes(-4), Now.AddDays(1), Now);

        Assert.Equal(2, period.BillableDays);
    }

    [Fact]
    public void Validate_MoreThanSixtyDays_ThrowsWithErrorCode()
    {
        var ex = Assert.Throws<BadRequestException>(
            () => RentalPeriodCalculator.Validate(Start, Start.AddDays(60).AddMinutes(1), Now));

        Assert.Equal(ErrorCodes.InvalidRentalPeriod, ex.ErrorCode);
    }

    [Fact]
    public void Validate_ExactlySixtyDays_IsAccepted()
    {
        var period = RentalPeriodCalculator.Validate(Start, Start.AddDays(60), Now);

        Assert.Equal(60, period.BillableDays);
    }
}
