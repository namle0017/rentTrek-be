using RenTrek.BLL.Constants;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Tests.Helpers;

public sealed class RentalFulfillmentRulesTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static AssetAssignment Reserved(string code) => new()
    {
        AssetAssignmentId = Guid.NewGuid(),
        Status = AssignmentStatuses.Reserved,
        EquipmentAsset = new EquipmentAsset { AssetCode = code }
    };

    [Fact]
    public void CanClaim_StaffTakesUnassignedOrOwn_ButNotOthers()
    {
        Assert.True(RentalFulfillmentRules.CanClaim(null, Me, isAdmin: false));
        Assert.True(RentalFulfillmentRules.CanClaim(Me, Me, isAdmin: false));
        Assert.False(RentalFulfillmentRules.CanClaim(Other, Me, isAdmin: false));
    }

    [Fact]
    public void CanClaim_AdminTakesOverOthers()
        => Assert.True(RentalFulfillmentRules.CanClaim(Other, Me, isAdmin: true));

    [Theory]
    [InlineData(RentalChargeStatuses.Draft, false, true)]
    [InlineData(RentalChargeStatuses.Draft, true, true)]
    [InlineData(RentalChargeStatuses.Assessed, false, false)] // đã thành công nợ: Staff không hủy được
    [InlineData(RentalChargeStatuses.Assessed, true, true)]
    [InlineData(RentalChargeStatuses.Voided, true, false)]
    [InlineData(RentalChargeStatuses.Settled, true, false)]
    public void CanVoid_AssessedOnlyByAdmin(string status, bool isAdmin, bool expected)
        => Assert.Equal(expected, RentalFulfillmentRules.CanVoid(status, isAdmin));

    [Fact]
    public void PickByCode_MatchesTrimmedCaseInsensitive()
    {
        var tent = Reserved("TENT-001");
        var picked = RentalFulfillmentRules.PickByCode([Reserved("POLE-001"), tent], "  tent-001 ");
        Assert.Same(tent, Assert.Single(picked));
    }

    [Fact]
    public void PickByCode_RejectsCodeNotReservedForThisOrder()
    {
        // Mã của đơn khác, hoặc mã đã giao (không còn Reserved) đều không có trong danh sách.
        var ex = Assert.Throws<ConflictException>(() => RentalFulfillmentRules.PickByCode([Reserved("TENT-001")], "TENT-999"));
        Assert.Contains("TENT-999", ex.Message);
    }

    [Fact]
    public void PickForHandover_EmptySelectionTakesAll()
    {
        List<AssetAssignment> reserved = [Reserved("A"), Reserved("B")];
        Assert.Same(reserved, RentalFulfillmentRules.PickForHandover(reserved, null));
        Assert.Same(reserved, RentalFulfillmentRules.PickForHandover(reserved, []));
    }

    [Fact]
    public void PickForHandover_TakesSelectedSubset()
    {
        var a = Reserved("A");
        var picked = RentalFulfillmentRules.PickForHandover([a, Reserved("B")], [a.AssetAssignmentId]);
        Assert.Same(a, Assert.Single(picked));
    }

    [Fact]
    public void PickForHandover_RejectsIdNotReserved()
        => Assert.Throws<ConflictException>(
            () => RentalFulfillmentRules.PickForHandover([Reserved("A")], [Guid.NewGuid()]));
}
