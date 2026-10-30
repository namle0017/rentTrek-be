using Microsoft.Extensions.Options;
using RenTrek.BLL.DTOs.Auth;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ auth.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtHelper _jwtHelper;
    private readonly JwtSettings _jwt;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtHelper jwtHelper,
        IOptions<JwtSettings> jwtOptions)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtHelper = jwtHelper;
        _jwt = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _unitOfWork.Accounts.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException($"Email '{request.Email}' đã được đăng ký tài khoản trong hệ thống");
        }

        var customerRole = await _unitOfWork.Roles.GetByNameAsync("Customer", tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Chưa có role Customer trong bảng roles");

        var account = new Account
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = customerRole.RoleId,
            Role = customerRole,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Accounts.AddAsync(account, cancellationToken);
        return await IssueTokensAsync(account, Guid.NewGuid(), replaces: null, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var account = await _unitOfWork.Accounts.GetByEmailWithRoleAsync(normalizedEmail, tracking: true, cancellationToken);

        if (account is null || !_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            throw new UnauthorizedException("Email hoặc mật khẩu không chính xác");
        }

        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");
        }

        return await IssueTokensAsync(account, Guid.NewGuid(), replaces: null, cancellationToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtHelper.HashToken(request.RefreshToken);

        var existingToken = await _unitOfWork.RefreshTokens.GetByTokenHashWithUserAsync(tokenHash, tracking: true, cancellationToken)
            ?? throw new UnauthorizedException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn");

        // Reuse detection: Nếu token đã từng bị revoke mà gửi lại -> Phát hiện tấn công đánh cắp token
        if (existingToken.RevokedAt is not null)
        {
            await _unitOfWork.RefreshTokens.RevokeFamilyTokensAsync(
                existingToken.FamilyId,
                "Compromised family - token reuse detected",
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedException("Phát hiện bất thường về bảo mật phiên. Toàn bộ phiên làm việc đã bị hủy, vui lòng đăng nhập lại.");
        }

        if (existingToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
        }

        if (!string.Equals(existingToken.Account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");
        }

        // Đánh dấu thu hồi token cũ do xoay vòng
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedReason = "Replaced by rotation";

        return await IssueTokensAsync(existingToken.Account, existingToken.FamilyId, existingToken, cancellationToken);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return;

        var tokenHash = _jwtHelper.HashToken(request.RefreshToken);

        var token = await _unitOfWork.RefreshTokens.GetByTokenHashWithAccessTokensAsync(tokenHash, tracking: true, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "User logged out";

            foreach (var accessToken in token.AccessTokens.Where(at => at.RevokedAt == null))
            {
                accessToken.RevokedAt = DateTime.UtcNow;
                accessToken.RevokedReason = "User logged out";
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // Tạo cặp refresh/access token mới; lưu cùng các thay đổi đang chờ trong một lần SaveChanges
    private async Task<AuthResponse> IssueTokensAsync(
        Account account,
        Guid familyId,
        RefreshToken? replaces,
        CancellationToken cancellationToken)
    {
        var rawRefreshToken = _jwtHelper.GenerateRefreshToken();
        var refreshExpiresDays = _jwt.RefreshTokenExpirationDays > 0 ? _jwt.RefreshTokenExpirationDays : 7;

        var refreshToken = new RefreshToken
        {
            UserId = account.UserId,
            FamilyId = familyId,
            TokenHash = _jwtHelper.HashToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays),
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);

        if (replaces is not null)
            replaces.ReplacedByRefreshTokenId = refreshToken.RefreshTokenId;

        var (accessToken, jti, accessExpiresAt) = _jwtHelper.GenerateAccessToken(account);
        await _unitOfWork.AccessTokens.AddAsync(new AccessToken
        {
            UserId = account.UserId,
            RefreshTokenId = refreshToken.RefreshTokenId,
            Jti = jti,
            ExpiresAt = accessExpiresAt,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAt = accessExpiresAt,
            User = new UserInfoResponse
            {
                UserId = account.UserId,
                Email = account.Email,
                FullName = account.FullName,
                Role = account.Role?.RoleName ?? "Customer",
                AvatarUrl = account.Profile?.AvatarUrl
            }
        };
    }
}
