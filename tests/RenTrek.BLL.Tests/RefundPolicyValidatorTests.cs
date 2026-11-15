using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.Validators.Refund;

namespace RenTrek.BLL.Tests;

/// <summary>Test thuần cho validator chính sách hoàn tiền (TC-RP-V01..V21).</summary>
public sealed class RefundPolicyValidatorTests
{
    private static readonly DateOnly From = new(2026, 12, 1);

    private static RefundPolicyRuleRequest Rule(int hours, decimal percent) => new() { MinHoursBefore = hours, RefundPercentage = percent };

    private static List<RefundPolicyRuleRequest> ValidRules() => [Rule(168, 100), Rule(72, 80), Rule(24, 50), Rule(0, 0)];

    private static SaveRefundPolicyRequest Make(
        string name = "Chính sách Tour", string type = "Tour", string? description = null,
        DateOnly? from = null, DateOnly? until = null, List<RefundPolicyRuleRequest>? rules = null, bool draft = false)
        => new()
        {
            Name = name,
            PolicyType = type,
            Description = description,
            EffectiveFrom = from ?? From,
            EffectiveUntil = until,
            Rules = rules ?? ValidRules(),
            SaveAsDraft = draft
        };

    private static bool Valid(SaveRefundPolicyRequest request) => new SaveRefundPolicyRequestValidator().Validate(request).IsValid;
    private static bool Valid(List<RefundPolicyRuleRequest> rules) => new RefundPolicyRulesRequestValidator().Validate(new RefundPolicyRulesRequest { Rules = rules }).IsValid;
    private static bool ValidBoth(List<RefundPolicyRuleRequest> rules) => Valid(Make(rules: rules)) == Valid(rules) ? Valid(rules) : throw new Xunit.Sdk.XunitException("Save and Rules validators disagree");

    [Fact]
    public void V01_ValidRequest() => Assert.True(Valid(Make()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void V02_NameRequired(string name) => Assert.False(Valid(Make(name: name)));

    [Fact]
    public void V03_NameMax200()
    {
        Assert.True(Valid(Make(name: new string('a', 200))));
        Assert.False(Valid(Make(name: new string('a', 201))));
    }

    [Theory]
    [InlineData("Tour", true)]
    [InlineData("Rental", true)]
    [InlineData("Other", false)]
    [InlineData("", false)]
    public void V04_PolicyType(string type, bool expected) => Assert.Equal(expected, Valid(Make(type: type)));

    [Fact]
    public void V05_DescriptionMax2000()
    {
        Assert.True(Valid(Make(description: null)));
        Assert.True(Valid(Make(description: new string('d', 2000))));
        Assert.False(Valid(Make(description: new string('d', 2001))));
    }

    [Fact]
    public void V06_EffectiveUntilMustBeAfterFrom()
    {
        Assert.False(Valid(Make(until: From)));
        Assert.False(Valid(Make(until: From.AddDays(-1))));
        Assert.True(Valid(Make(until: From.AddDays(1))));
        Assert.True(Valid(Make(until: null)));
    }

    [Fact]
    public void V07_EffectiveFromRequired() => Assert.False(Valid(Make(from: default(DateOnly))));

    [Fact]
    public void V08_RulesRequired()
    {
        Assert.False(Valid(Make(rules: [])));
        Assert.False(Valid(new List<RefundPolicyRuleRequest>()));
    }

    [Fact]
    public void V09_RulesMax20()
    {
        // 20 mốc: giờ 19..0, % giảm dần 100 -> 5
        var twenty = Enumerable.Range(0, 20).Select(i => Rule(i * 10, 5 * (i + 1))).ToList();
        var twentyOne = Enumerable.Range(0, 21).Select(i => Rule(i * 10, i)).ToList();
        Assert.True(ValidBoth(twenty));
        Assert.False(Valid(Make(rules: twentyOne)));
        Assert.False(Valid(twentyOne));
    }

    [Fact]
    public void V10_DuplicateHoursRejected() => Assert.False(ValidBoth([Rule(24, 50), Rule(24, 40)]));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(8760, true)]
    [InlineData(8761, false)]
    public void V11_HoursRange(int hours, bool expected) => Assert.Equal(expected, ValidBoth(hours == 0 ? [Rule(0, 50)] : [Rule(hours, 50), Rule(0, 0)]));

    [Theory]
    [InlineData("-0.01", false)]
    [InlineData("0", true)]
    [InlineData("100", true)]
    [InlineData("100.01", false)]
    public void V12_PercentRange(string percent, bool expected)
        => Assert.Equal(expected, ValidBoth([Rule(24, decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture)), Rule(0, 0)]));

    [Theory]
    [InlineData("Tour")]
    [InlineData("Rental")]
    public void MissingZeroHourTierRejectedForBothPolicyTypes(string type)
    {
        List<RefundPolicyRuleRequest> rules = [Rule(168, 100), Rule(24, 50)];
        Assert.False(Valid(Make(type: type, rules: rules)));
        Assert.False(Valid(rules));
    }

    [Theory]
    [InlineData("Tour", false)]
    [InlineData("Rental", true)]
    public void TourTermsAreValidatedOnlyForTour(string type, bool expected)
    {
        var request = Make(type: type);
        request.RescheduleMinHoursBefore = -1;
        request.RescheduleFee = -1;
        request.MaxReschedules = -1;
        request.NoShowGraceMinutes = -1;

        Assert.Equal(expected, Valid(request));
    }

    [Fact]
    public void V13_NonIncreasingTowardDeparture() => Assert.True(ValidBoth(ValidRules()));

    [Fact]
    public void V14_IncreasingTowardDepartureRejected() => Assert.False(ValidBoth([Rule(168, 50), Rule(72, 80)]));

    [Fact]
    public void V15_EqualPercentAllowed() => Assert.True(ValidBoth([Rule(72, 50), Rule(24, 50), Rule(0, 50)]));

    [Fact]
    public void V16_UnorderedInputIsSortedByValidator() => Assert.True(ValidBoth([Rule(0, 0), Rule(168, 100), Rule(24, 50), Rule(72, 80)]));

    [Fact]
    public void V17_RulesRequestValidatorMatchesSaveValidator()
    {
        Assert.True(Valid(ValidRules()));
        Assert.False(Valid([Rule(24, 50), Rule(24, 50)]));
        Assert.False(Valid([Rule(168, 10), Rule(0, 90)]));
        Assert.False(Valid([Rule(-5, 10)]));
        Assert.False(Valid([Rule(5, 101)]));
    }

    private static bool ValidQuery(RefundPolicyQueryRequest q) => new RefundPolicyQueryRequestValidator().Validate(q).IsValid;

    [Theory]
    [InlineData("createdAt", true)]
    [InlineData("name", true)]
    [InlineData("effectiveFrom", true)]
    [InlineData("version", true)]
    [InlineData("evil", false)]
    public void V18_SortByWhitelist(string sortBy, bool expected) => Assert.Equal(expected, ValidQuery(new() { SortBy = sortBy }));

    [Theory]
    [InlineData("asc", true)]
    [InlineData("desc", true)]
    [InlineData("up", false)]
    public void V19_SortOrderWhitelist(string order, bool expected) => Assert.Equal(expected, ValidQuery(new() { SortOrder = order }));

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Scheduled", true)]
    [InlineData("Active", true)]
    [InlineData("Expired", true)]
    [InlineData("Archived", true)]
    [InlineData("x", false)]
    public void V20_StatusWhitelist(string status, bool expected) => Assert.Equal(expected, ValidQuery(new() { Status = status }));

    [Fact]
    public void V21_SearchMax200()
    {
        Assert.True(ValidQuery(new() { Search = new string('s', 200) }));
        Assert.False(ValidQuery(new() { Search = new string('s', 201) }));
    }

    [Fact]
    public void Query_DefaultsAreValid_AndLatestOnlyDefaultsToTrue()
    {
        var query = new RefundPolicyQueryRequest();
        Assert.True(ValidQuery(query));
        Assert.True(query.LatestOnly);
    }
}
