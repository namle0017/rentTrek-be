using RenTrek.BLL.Security;

namespace RenTrek.BLL.Tests;

public sealed class ParticipantInviteTokenServiceTests
{
    [Fact]
    public void CreateToken_ReturnsDifferentUrlSafeTokens()
    {
        var service = new ParticipantInviteTokenService();

        var first = service.CreateToken();
        var second = service.CreateToken();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }

    [Fact]
    public void HashToken_ReturnsDeterministicSha256HexWithoutPlaintext()
    {
        var service = new ParticipantInviteTokenService();

        var hash = service.HashToken("invite-token");

        Assert.Equal("F9E3C47D452A8FAB2DC56EF07D766534CB2CD31C5F63DE7107412ACC65DAA5B8", hash);
        Assert.DoesNotContain("invite-token", hash, StringComparison.OrdinalIgnoreCase);
    }
}
