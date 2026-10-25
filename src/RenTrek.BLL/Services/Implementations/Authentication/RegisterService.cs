using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.BLL.Services.Interfaces.Authentication;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations.Authentication;

public class RegisterService : IRegisterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly JwtSettings _jwt;

    public RegisterService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ICurrentUserService currentUserService,
        IOptions<JwtSettings> jwtOptions)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _currentUserService = currentUserService;
        _jwt = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _unitOfWork.Repository<User>()
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw new ConflictException($"Email '{request.Email}' đã được đăng ký tài khoản trong hệ thống");
        }

        var customerRole = await _unitOfWork.Repository<Role>()
            .QueryTracking()
            .FirstOrDefaultAsync(r => r.RoleName == "Customer", cancellationToken);

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = customerRole?.RoleId ?? 3,
            Role = customerRole!,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var (accessToken, jti, accessExpiresAt) = _tokenService.GenerateAccessToken(user);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashToken(rawRefreshToken);
        var familyId = Guid.NewGuid().ToString("N");
        var refreshExpiresDays = _jwt.RefreshTokenExpirationDays > 0 ? _jwt.RefreshTokenExpirationDays : 7;
        var refreshExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays);

        var deviceInfo = DeviceDetector.Detect(_currentUserService.UserAgent);
        var ipAddress = _currentUserService.IpAddress;

        var refreshToken = new RefreshToken
        {
            UserId = user.UserId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };

        await _unitOfWork.Repository<RefreshToken>().AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessTokenEntity = new AccessToken
        {
            UserId = user.UserId,
            RefreshTokenId = refreshToken.RefreshTokenId,
            Jti = jti,
            ExpiresAt = accessExpiresAt,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<AccessToken>().AddAsync(accessTokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAt = accessExpiresAt,
            User = new UserInfoResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role?.RoleName ?? "Customer"
            }
        };
    }
}
