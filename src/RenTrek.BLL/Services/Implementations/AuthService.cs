using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.BLL.DTOs.Auth.Requests;
using RenTrek.BLL.DTOs.Auth.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ auth, xác thực email và tự gán các chuyến đi đã thanh toán theo email.
/// </summary>
public class AuthService : IAuthService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtHelper _jwtHelper;
    private readonly JwtSettings _jwt;
    private readonly TimeProvider _timeProvider;
    private readonly IInviteTokenHelper _inviteTokenHelper;
    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtHelper jwtHelper,
        IOptions<JwtSettings> jwtOptions,
        TimeProvider timeProvider,
        IInviteTokenHelper inviteTokenHelper,
        IEmailSender emailSender,
        IOptions<EmailSettings> emailOptions,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtHelper = jwtHelper;
        _jwt = jwtOptions.Value;
        _timeProvider = timeProvider;
        _inviteTokenHelper = inviteTokenHelper;
        _emailSender = emailSender;
        _emailSettings = emailOptions.Value;
        _logger = logger;
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

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var account = new Account
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = customerRole.RoleId,
            Role = customerRole,
            Status = "Active",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await _unitOfWork.Accounts.AddAsync(account, cancellationToken);
        var response = await IssueTokensAsync(account, Guid.NewGuid(), replaces: null, cancellationToken);
        await TrySendVerificationEmailAsync(account, cancellationToken);
        return response;
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

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (existingToken.ExpiresAt <= utcNow)
        {
            throw new UnauthorizedException("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
        }

        if (!string.Equals(existingToken.Account.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");
        }

        // Đánh dấu thu hồi token cũ do xoay vòng
        existingToken.RevokedAt = utcNow;
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
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            token.RevokedAt = utcNow;
            token.RevokedReason = "User logged out";

            foreach (var accessToken in token.AccessTokens.Where(at => at.RevokedAt == null))
            {
                accessToken.RevokedAt = utcNow;
                accessToken.RevokedReason = "User logged out";
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow();
        if (!_inviteTokenHelper.TryParseEmailVerificationToken(request.Token, out var userId, out _))
            throw new BadRequestException("Mã xác thực email không hợp lệ");

        var account = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: true, cancellationToken)
            ?? throw new BadRequestException("Mã xác thực email không hợp lệ");

        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");

        var normalizedEmail = account.Email.Trim().ToLowerInvariant();
        if (!_inviteTokenHelper.VerifyEmailVerificationToken(request.Token, account.UserId, normalizedEmail, utcNow))
            throw new BadRequestException("Mã xác thực email không hợp lệ hoặc đã hết hạn");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            account.EmailConfirmedAt ??= utcNow.UtcDateTime;
            account.UpdatedAt = utcNow.UtcDateTime;

            var seats = await _unitOfWork.TripBookings.GetUnlinkedPaidParticipantsByEmailAsync(
                normalizedEmail,
                utcNow,
                cancellationToken);

            foreach (var seat in seats)
            {
                if (seat.LinkedUserId is not null)
                    continue;
                if (seat.BookingOrder?.Order is { } order &&
                    (!string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                     order.IsDeleted))
                {
                    continue;
                }
                if (seat.Trip is not null && seat.Trip.StartAt <= utcNow)
                    continue;

                seat.LinkedUserId = account.UserId;
                seat.UpdatedAt = utcNow;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: false, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản");

        if (!string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");

        if (account.EmailConfirmedAt is not null)
            return;

        await TrySendVerificationEmailAsync(account, cancellationToken);
    }

    private async Task TrySendVerificationEmailAsync(Account account, CancellationToken cancellationToken)
    {
        if (!_emailSettings.IsConfigured)
        {
            _logger.LogWarning("Chưa cấu hình Email SMTP, chưa gửi được link xác thực email cho user {UserId}", account.UserId);
            return;
        }

        var normalizedEmail = account.Email.Trim().ToLowerInvariant();
        var expiresAt = _timeProvider.GetUtcNow().Add(VerificationTokenLifetime);
        var token = _inviteTokenHelper.CreateEmailVerificationToken(account.UserId, normalizedEmail, expiresAt);
        var verifyUrl = $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}/verify-email#t={token}";

        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:520px;margin:auto;color:#1f2937">
              <h2 style="color:#1f4d3a">Xác thực địa chỉ email</h2>
              <p>Xin chào <b>{WebUtility.HtmlEncode(account.FullName)}</b>,</p>
              <p>Vui lòng xác thực email để kích hoạt tính năng xem lịch các chuyến đi của bạn trên RenTrek.</p>
              <p><a href="{WebUtility.HtmlEncode(verifyUrl)}" style="display:inline-block;background:#1f4d3a;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none">Xác thực email</a></p>
              <p style="font-size:13px;color:#6b7280">Link có hiệu lực trong 24 giờ.</p>
            </div>
            """;

        try
        {
            await _emailSender.SendAsync(
                account.Email,
                account.FullName,
                "Xác thực email tài khoản RenTrek",
                body,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gửi email xác thực thất bại cho user {UserId}", account.UserId);
        }
    }

    // Tạo cặp refresh/access token mới; lưu cùng các thay đổi đang chờ trong một lần SaveChanges
    private async Task<AuthResponse> IssueTokensAsync(
        Account account,
        Guid familyId,
        RefreshToken? replaces,
        CancellationToken cancellationToken)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var rawRefreshToken = _jwtHelper.GenerateRefreshToken();
        var refreshExpiresDays = _jwt.RefreshTokenExpirationDays > 0 ? _jwt.RefreshTokenExpirationDays : 7;

        var refreshToken = new RefreshToken
        {
            UserId = account.UserId,
            FamilyId = familyId,
            TokenHash = _jwtHelper.HashToken(rawRefreshToken),
            ExpiresAt = utcNow.AddDays(refreshExpiresDays),
            CreatedAt = utcNow
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
            CreatedAt = utcNow
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
                EmailConfirmed = account.EmailConfirmedAt is not null
            }
        };
    }
}
