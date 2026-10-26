using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RenTrek.API.Identity;
using RenTrek.BLL.Exceptions;

namespace RenTrek.API.Tests;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void UserId_ReadsGuidFromSubjectClaim()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
                "test"))
        };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(userId, service.UserId);
    }

    [Fact]
    public void UserId_RejectsMissingSubjectClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"))
        };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        var exception = Assert.Throws<UnauthorizedException>(() => service.RequiredUserId);

        Assert.Equal("Token không chứa định danh tài khoản hợp lệ", exception.Message);
    }
}
