using Microsoft.Extensions.Options;
using RenTrek.BLL.Helpers.Implementations;
using RenTrek.BLL.Options;

namespace RenTrek.BLL.Tests;

public sealed class InviteTokenHelperTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);

    private static InviteTokenHelper CreateHelper(string key)
        => new(Microsoft.Extensions.Options.Options.Create(new InviteSettings { SigningKey = key }));

    [Fact]
    public void CreateGroupToken_IsStableAndMatchesRequestFormat()
    {
        var helper = CreateHelper(new string('k', 32));
        var orderId = Guid.NewGuid();

        var token = helper.CreateGroupToken(orderId, 0);

        Assert.Equal(token, helper.CreateGroupToken(orderId, 0));
        Assert.Matches("^[0-9A-F]{64}$", token);
    }

    [Fact]
    public void CreateGroupToken_ChangesWithVersionOrderAndKey()
    {
        var helper = CreateHelper(new string('k', 32));
        var orderId = Guid.NewGuid();
        var token = helper.CreateGroupToken(orderId, 0);

        Assert.NotEqual(token, helper.CreateGroupToken(orderId, 1));
        Assert.NotEqual(token, helper.CreateGroupToken(Guid.NewGuid(), 0));
        Assert.NotEqual(token, CreateHelper(new string('x', 32)).CreateGroupToken(orderId, 0));
    }

    [Fact]
    public void CreateGroupToken_RefusesMissingOrShortKey()
    {
        Assert.Throws<InvalidOperationException>(() => CreateHelper(string.Empty).CreateGroupToken(Guid.NewGuid(), 0));
        Assert.Throws<InvalidOperationException>(() => CreateHelper("short").CreateGroupToken(Guid.NewGuid(), 0));
    }

    [Fact]
    public void EmailVerificationToken_VerifiesUntilExpiryAndRejectsMismatch()
    {
        var helper = CreateHelper(new string('k', 32));
        var userId = Guid.NewGuid();
        var expiresAt = Now.AddHours(24);

        var token = helper.CreateEmailVerificationToken(userId, "user@example.com", expiresAt);

        Assert.True(helper.TryParseEmailVerificationToken(token, out var parsedUserId, out _));
        Assert.Equal(userId, parsedUserId);
        Assert.True(helper.VerifyEmailVerificationToken(token, userId, "user@example.com", Now));
        Assert.False(helper.VerifyEmailVerificationToken(token, userId, "other@example.com", Now));
        Assert.False(helper.VerifyEmailVerificationToken(token, Guid.NewGuid(), "user@example.com", Now));
        Assert.False(helper.VerifyEmailVerificationToken(token, userId, "user@example.com", expiresAt));
        Assert.False(CreateHelper(new string('x', 32)).VerifyEmailVerificationToken(token, userId, "user@example.com", Now));
    }
}
