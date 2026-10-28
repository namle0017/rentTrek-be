using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Rental.Internal;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;

namespace RenTrek.BLL.Tests;

public sealed class RentalAvailabilityServiceTests
{
    // Các nhánh dưới đây không chạm repository nên không cần UnitOfWork thật.
    private static RentalAvailabilityService CreateService() => new(null!, TimeProvider.System);

    private static RentalPeriodResult Period() => new()
    {
        StartUtc = new DateTime(2026, 10, 24, 1, 0, 0, DateTimeKind.Utc),
        EndUtc = new DateTime(2026, 10, 26, 1, 0, 0, DateTimeKind.Utc),
        BillableDays = 2
    };

    [Fact]
    public void BuildLineMessage_ReturnsEmpty_WhenNoShortage()
    {
        var message = CreateService().BuildLineMessage(new RentalLineAvailabilityResult { IsAvailable = true }, Period());

        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void BuildLineMessage_UsesVietnamDatesAndAvailableCount()
    {
        var line = new RentalLineAvailabilityResult
        {
            Shortages =
            [
                new RentalAvailabilityShortageItem { VariantName = "Đệm hơi", Requested = 2, Available = 1 },
                new RentalAvailabilityShortageItem { VariantName = "Túi ngủ", Requested = 1, Available = 0 }
            ]
        };

        var message = CreateService().BuildLineMessage(line, Period());

        Assert.Equal("Chỉ còn 1 Đệm hơi trong 24/10 – 26/10. Túi ngủ đã hết trong 24/10 – 26/10.", message);
    }

    [Fact]
    public async Task ResolveLinesAsync_Throws_WhenNoLines()
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => CreateService().ResolveLinesAsync([]));

        Assert.Equal(ErrorCodes.InvalidRentalItem, exception.ErrorCode);
    }

    [Theory]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 0)]
    public async Task ResolveLinesAsync_Throws_WhenLineShapeInvalid(bool hasVariant, bool hasBundle, int quantity)
    {
        var line = new RentalLineInput
        {
            ProductVariantId = hasVariant ? Guid.NewGuid() : null,
            BundleId = hasBundle ? Guid.NewGuid() : null,
            Quantity = quantity
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => CreateService().ResolveLinesAsync([line]));

        Assert.Equal(ErrorCodes.InvalidRentalItem, exception.ErrorCode);
    }
}
