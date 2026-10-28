using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Promotion;
using RenTrek.BLL.DTOs.Rental;
using RenTrek.BLL.DTOs.RentalOrder;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Validators.RentalOrder;

namespace RenTrek.BLL.Tests;

public sealed class RentalOrderServiceTests
{
    // Các nhánh dưới đây dừng trước khi chạm repository nên không cần UnitOfWork thật.
    private static RentalOrderService CreateService() => new(null!, null!, null!, TimeProvider.System);

    private static CreateRentalOrderRequest ValidRequest() => new()
    {
        RentalStartAt = DateTime.UtcNow.AddDays(1),
        RentalEndAt = DateTime.UtcNow.AddDays(3),
        Items = [new RentalItemRequest { ProductVariantId = Guid.NewGuid(), Quantity = 1 }],
        AcceptedRefundPolicyVersion = 1
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_Throws_WhenIdempotencyKeyMissing(string? key)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CreateAsync(Guid.NewGuid(), ValidRequest(), key));

        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, exception.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenIdempotencyKeyTooLong()
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CreateAsync(Guid.NewGuid(), ValidRequest(), new string('a', 101)));

        Assert.Equal(ErrorCodes.ValidationError, exception.ErrorCode);
    }

    [Fact]
    public async Task PromotionService_ReturnsNotApplied_WhenCodeBlank()
    {
        var service = new PromotionService(null!);

        var result = await service.EvaluateAsync(new EvaluatePromotionRequest { PromotionCode = "  " });

        Assert.False(result.IsApplied);
        Assert.Null(result.ErrorCode);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public void CreateValidator_AcceptsValidRequest()
    {
        var result = new CreateRentalOrderRequestValidator().Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateValidator_RejectsBadItemsPickupAndPolicyVersion()
    {
        var request = ValidRequest();
        request.Items = [new RentalItemRequest { ProductVariantId = Guid.NewGuid(), Quantity = 21 }];
        request.Note = new string('n', 1001);
        request.AcceptedRefundPolicyVersion = 0;
        request.Pickup = new RentalPickupRequest { ReceiverPhone = "123", Method = "Delivery" };

        var result = new CreateRentalOrderRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.Contains("Quantity"));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateRentalOrderRequest.Note));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateRentalOrderRequest.AcceptedRefundPolicyVersion));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("ReceiverPhone"));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("Method"));
    }

    [Fact]
    public void QuoteValidator_RejectsEmptyItemsAndLongPromotionCode()
    {
        var request = new RentalQuoteRequest
        {
            RentalStartAt = DateTime.UtcNow.AddDays(1),
            RentalEndAt = DateTime.UtcNow.AddDays(2),
            Items = [],
            PromotionCode = new string('x', 51)
        };

        var result = new RentalQuoteRequestValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RentalQuoteRequest.Items));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RentalQuoteRequest.PromotionCode));
    }
}
