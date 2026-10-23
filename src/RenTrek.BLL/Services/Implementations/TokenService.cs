using Microsoft.Extensions.Options;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Implementations;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;

    public TokenService(IOptions<JwtSettings> jwtOptions)
    {
        _jwt = jwtOptions.Value;
    }

    // TODO: claims (sub, email, role, jti) va ky HMAC-SHA256 bang _jwt.SecretKey
    public (string Token, string Jti, DateTime ExpiresAt) GenerateAccessToken(User user)
        => throw new NotImplementedException();

    // TODO: RandomNumberGenerator -> Base64
    public string GenerateRefreshToken() => throw new NotImplementedException();

    // TODO: SHA256 -> Base64
    public string HashToken(string token) => throw new NotImplementedException();
}
