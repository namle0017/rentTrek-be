using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Tests;

public sealed class AdminRequestValidatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PromotionTierRequest Tier(string metric = "OrderAmount", decimal min = 1000, string type = "Percentage",
        decimal value = 10, Guid? gift = null, int? qty = null) => new(metric, min, type, value, gift, qty);

    private static bool Valid(PromotionTiersRequest r) => new PromotionTiersRequestValidator().Validate(r).IsValid;

    [Fact]
    public void Reason_BlankRejected()
    {
        Assert.False(new ReasonRequestValidator().Validate(new ReasonRequest("  ")).IsValid);
        Assert.True(new ReasonRequestValidator().Validate(new ReasonRequest("Vi phạm")).IsValid);
    }

    [Fact]
    public void Promotion_EndBeforeStartOrNonUtcRejected()
    {
        var validator = new CreatePromotionRequestValidator();
        CreatePromotionRequest Make(DateTime end) => new("Hè", "SUMMER", null, Start, end, "All", "Order", null, null, false, 0);
        Assert.True(validator.Validate(Make(Start.AddDays(1))).IsValid);
        Assert.False(validator.Validate(Make(Start)).IsValid);
        Assert.False(validator.Validate(Make(DateTime.SpecifyKind(Start.AddDays(1), DateTimeKind.Local))).IsValid);
    }

    [Fact]
    public void Tiers_Rules()
    {
        Assert.True(Valid(new([Tier()])));
        Assert.False(Valid(new([])));
        Assert.False(Valid(new([Tier(), Tier()])));                                  // trùng điều kiện
        Assert.False(Valid(new([Tier(value: 101)])));                                // > 100%
        Assert.False(Valid(new([Tier(metric: "RentalDays", min: 1.5m)])));           // ngày phải nguyên
        Assert.False(Valid(new([Tier(type: "GiftItem")])));                          // thiếu quà
        Assert.True(Valid(new([Tier(type: "GiftItem", gift: Guid.NewGuid(), qty: 1)])));
        Assert.False(Valid(new([Tier(gift: Guid.NewGuid(), qty: 1)])));              // quà ở bậc không phải GiftItem
    }

    [Theory]
    [InlineData(1000000, "Cash", true)]
    [InlineData(1000.5, "Cash", false)]
    [InlineData(0, "Cash", false)]
    [InlineData(1000, "Card", false)]
    public void ManualPayment_AmountAndMethod(decimal amount, string method, bool expected)
        => Assert.Equal(expected, new ManualPaymentRequestValidator().Validate(new ManualPaymentRequest(amount, method, "MANUAL-1", "Thu tại quầy")).IsValid);

    [Theory]
    [InlineData("staff@rentrek.vn", "Staff", "Passw0rd!", "0912345678", true)]
    [InlineData("not-an-email", "Staff", "Passw0rd!", null, false)]
    [InlineData("staff@rentrek.vn", "Admin", "Passw0rd!", null, false)]
    [InlineData("staff@rentrek.vn", "Guide", "password", null, false)]
    [InlineData("staff@rentrek.vn", "Guide", "Passw0rd!", "0123", false)]
    public void CreateStaff_Rules(string email, string role, string password, string? phone, bool expected)
        => Assert.Equal(expected, new CreateStaffRequestValidator().Validate(new CreateStaffRequest(email, "Nhân viên", phone, role, password, null)).IsValid);

    [Theory]
    [InlineData("leo-nui", true)]
    [InlineData("Leo Nui", false)]
    [InlineData("leo--nui", false)]
    public void Tag_SlugFormat(string slug, bool expected)
    {
        Assert.Equal(expected, new CreateTagRequestValidator().Validate(new CreateTagRequest("Leo núi", slug)).IsValid);
        Assert.Equal(expected, new UpdateTagRequestValidator().Validate(new UpdateTagRequest("Leo núi", slug)).IsValid);
    }
}
