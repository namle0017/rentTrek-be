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

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly JwtSettings _jwt;

    public RefreshTokenService(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ICurrentUserService currentUserService,
        IOptions<JwtSettings> jwtOptions)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _currentUserService = currentUserService;
        _jwt = jwtOptions.Value;
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var existingToken = await _unitOfWork.Repository<RefreshToken>()
            .QueryTracking()
            .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            throw new UnauthorizedException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn");
        }

        // Reuse detection: Nếu token đã từng bị revoke mà gửi lại -> Phát hiện tấn công đánh cắp token
        if (existingToken.RevokedAt is not null)
        {
            var familyTokens = await _unitOfWork.Repository<RefreshToken>()
                .QueryTracking()
                .Where(rt => rt.FamilyId == existingToken.FamilyId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in familyTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedReason = "Compromised family - token reuse detected";
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedException("Phát hiện bất thường về bảo mật phiên. Toàn bộ phiên làm việc đã bị hủy, vui lòng đăng nhập lại.");
        }

        if (existingToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
        }

        if (!string.Equals(existingToken.User.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");
        }

        // Đánh dấu thu hồi token cũ do xoay vòng
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedReason = "Replaced by rotation";

        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var newTokenHash = _tokenService.HashToken(rawRefreshToken);
        var refreshExpiresDays = _jwt.RefreshTokenExpirationDays > 0 ? _jwt.RefreshTokenExpirationDays : 7;
        var refreshExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays);

        var deviceInfo = !string.IsNullOrWhiteSpace(_currentUserService.UserAgent)
            ? DeviceDetector.Detect(_currentUserService.UserAgent)
            : existingToken.DeviceInfo ?? "Trình duyệt web";
        var ipAddress = _currentUserService.IpAddress ?? existingToken.IpAddress;

        var newRefreshToken = new RefreshToken
        {
            UserId = existingToken.UserId,
            FamilyId = existingToken.FamilyId,
            TokenHash = newTokenHash,
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };

        await _unitOfWork.Repository<RefreshToken>().AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        existingToken.ReplacedByRefreshTokenId = newRefreshToken.RefreshTokenId;

        var (accessToken, jti, accessExpiresAt) = _tokenService.GenerateAccessToken(existingToken.User);

        var accessTokenEntity = new AccessToken
        {
            UserId = existingToken.UserId,
            RefreshTokenId = newRefreshToken.RefreshTokenId,
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
                UserId = existingToken.User.UserId,
                Email = existingToken.User.Email,
                FullName = existingToken.User.FullName,
                Role = existingToken.User.Role?.RoleName ?? "Customer"
            }
        };
    }
}
