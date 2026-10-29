using RenTrek.BLL.Helpers.Implementations;

namespace RenTrek.BLL.Tests;

public sealed class InviteTokenHelperTests
{
    [Fact]
    public void CreateCode_ReturnsSixDigits()
    {
        var helper = new InviteTokenHelper();

        var codes = Enumerable.Range(0, 50).Select(_ => helper.CreateCode()).ToList();

        Assert.All(codes, code => Assert.Matches(@"^\d{6}$", code));
        Assert.True(codes.Distinct().Count() > 1);
    }

    [Fact]
    public void HashToken_ReturnsDeterministicSha256HexWithoutPlaintext()
    {
        var helper = new InviteTokenHelper();

        var hash = helper.HashToken("invite-token");

        Assert.Equal("F9E3C47D452A8FAB2DC56EF07D766534CB2CD31C5F63DE7107412ACC65DAA5B8", hash);
        Assert.DoesNotContain("invite-token", hash, StringComparison.OrdinalIgnoreCase);
    }
}
