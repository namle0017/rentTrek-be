using System.Globalization;
using RenTrek.BLL.Constants;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.BLL.DTOs.Auth.Requests;
using RenTrek.BLL.DTOs.Auth.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ auth, xác thực email và tự gán các chuyến đi đã thanh toán theo email.
/// Hỗ trợ kiểm soát Brute-force và gửi email OTP qua MailKit SMTP.
/// </summary>
public class AuthService : IAuthService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtHelper _jwtHelper;
    private readonly IEmailService _emailService;
    private readonly JwtSettings _jwt;
    private readonly TimeProvider _timeProvider;
    private readonly IInviteTokenHelper _inviteTokenHelper;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuthAttemptLimiter _attemptLimiter;
    private readonly GoogleAuthSettings _google;

    // ponytail: OTP xác thực email nằm trong RAM, mất khi restart và không chia sẻ giữa nhiều instance; chuyển sang bảng DB nếu scale ngang
    private readonly IMemoryCache _cache;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtHelper jwtHelper,
        IOptions<JwtSettings> jwtOptions,
        TimeProvider timeProvider,
        IInviteTokenHelper inviteTokenHelper,
        IOptions<EmailSettings> emailOptions,
        ILogger<AuthService> logger,
        IEmailService emailService,
        IAuthAttemptLimiter attemptLimiter,
        IOptions<GoogleAuthSettings> googleOptions,
        IMemoryCache cache)
    {
        _attemptLimiter = attemptLimiter;
        _google = googleOptions.Value;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtHelper = jwtHelper;
        _emailService = emailService;
        _jwt = jwtOptions.Value;
        _timeProvider = timeProvider;
        _inviteTokenHelper = inviteTokenHelper;
        _emailSettings = emailOptions.Value;
        _logger = logger;
        _cache = cache;
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
            Status = AccountStatuses.Active,
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
        var attemptKey = "login:" + normalizedEmail;
        await EnsureNotLockedAsync(attemptKey, cancellationToken);

        var account = await _unitOfWork.Accounts.GetByEmailWithRoleAsync(normalizedEmail, tracking: true, cancellationToken);

        if (account is null || !_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            await _attemptLimiter.RecordFailureAsync(attemptKey, cancellationToken);
            throw new UnauthorizedException("Email hoặc mật khẩu không chính xác");
        }

        await _attemptLimiter.ResetAsync(attemptKey, cancellationToken);

        if (!string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");
        }

        return await IssueTokensAsync(account, Guid.NewGuid(), replaces: null, cancellationToken);
    }

    private async Task EnsureNotLockedAsync(string attemptKey, CancellationToken cancellationToken)
    {
        if (await _attemptLimiter.IsLockedAsync(attemptKey, cancellationToken))
        {
            throw new TooManyRequestsException(
                $"Bạn đã nhập sai quá {AuthAttemptLimiter.MaxFailures} lần. Vui lòng thử lại sau {AuthAttemptLimiter.LockDuration.TotalMinutes:0} phút",
                ErrorCodes.TooManyRequests);
        }
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

        if (!string.Equals(existingToken.Account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
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
        Account account;
        string normalizedEmail;

        // Trường hợp 1: Xác thực qua đường Link Token (/verify-email#t=...)
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            if (!_inviteTokenHelper.TryParseEmailVerificationToken(request.Token, out var userId, out _))
                throw new BadRequestException("Mã xác thực email không hợp lệ");

            account = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: true, cancellationToken)
                ?? throw new BadRequestException("Mã xác thực email không hợp lệ");

            normalizedEmail = account.Email.Trim().ToLowerInvariant();
            if (!_inviteTokenHelper.VerifyEmailVerificationToken(request.Token, account.UserId, normalizedEmail, utcNow))
                throw new BadRequestException("Mã xác thực email không hợp lệ hoặc đã hết hạn");
        }
        // Trường hợp 2: Xác thực qua Modal OTP 6 số khi vừa đăng ký xong
        else if (!string.IsNullOrWhiteSpace(request.Otp) && !string.IsNullOrWhiteSpace(request.Email))
        {
            normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var otpKey = $"email_verify_otp:{normalizedEmail}";
            var attemptKey = "verify:" + normalizedEmail;
            await EnsureNotLockedAsync(attemptKey, cancellationToken);

            if (!_cache.TryGetValue(otpKey, out (string CachedOtp, Guid UserId) cachedData) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(cachedData.CachedOtp),
                    System.Text.Encoding.ASCII.GetBytes(request.Otp.Trim())))
            {
                // Sai quá nhiều: hủy OTP để chặn đoán mò 6 chữ số, buộc xin mã mới
                if (await _attemptLimiter.RecordFailureAsync(attemptKey, cancellationToken))
                    _cache.Remove(otpKey);

                throw new BadRequestException("Mã xác thực OTP không chính xác hoặc đã hết hạn (15 phút)");
            }

            await _attemptLimiter.ResetAsync(attemptKey, cancellationToken);

            account = await _unitOfWork.Accounts.GetByIdWithRoleAsync(cachedData.UserId, tracking: true, cancellationToken)
                ?? throw new BadRequestException("Không tìm thấy tài khoản để xác thực");

            _cache.Remove(otpKey);
        }
        else
        {
            throw new BadRequestException("Vui lòng cung cấp mã OTP hoặc link xác thực email");
        }

        if (!string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");

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
                    (!string.Equals(order.Status, OrderStatuses.Confirmed, StringComparison.OrdinalIgnoreCase) ||
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

        if (!string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa hoặc chưa kích hoạt");

        if (account.EmailConfirmedAt is not null)
            return;

        await TrySendVerificationEmailAsync(account, cancellationToken);
    }

    public async Task ResendVerificationEmailByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var account = await _unitOfWork.Accounts.GetByEmailWithRoleAsync(normalizedEmail, tracking: false, cancellationToken);

        // Chống Email Enumeration: không tiết lộ email có tồn tại, bị khóa hay đã xác thực
        if (account is null ||
            account.EmailConfirmedAt is not null ||
            !string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
            return;

        await TrySendVerificationEmailAsync(account, cancellationToken);
    }

    private async Task TrySendVerificationEmailAsync(Account account, CancellationToken cancellationToken)
    {
        var normalizedEmail = account.Email.Trim().ToLowerInvariant();
        var expiresAt = _timeProvider.GetUtcNow().Add(VerificationTokenLifetime);

        // Sinh link token HMAC (hạn 24 giờ)
        string? verifyUrl = null;
        try
        {
            var token = _inviteTokenHelper.CreateEmailVerificationToken(account.UserId, normalizedEmail, expiresAt);
            verifyUrl = $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}/verify-email#t={token}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chưa tạo được InviteToken verification link cho user {UserId}", account.UserId);
        }

        // Sinh mã OTP 6 số ngẫu nhiên cryptographically secure (hạn 15 phút) lưu vào MemoryCache
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        _cache.Set($"email_verify_otp:{normalizedEmail}", (otp, account.UserId), TimeSpan.FromMinutes(15));

        // Một email duy nhất chứa cả OTP lẫn link (EmailService gửi qua MailKit, chưa cấu hình SMTP thì in console)
        try
        {
            await _emailService.SendEmailVerificationOtpAsync(account.Email, account.FullName, otp, verifyUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gửi email xác thực thất bại cho user {UserId}", account.UserId);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var account = await _unitOfWork.Accounts.GetByEmailWithRoleAsync(normalizedEmail, tracking: true, cancellationToken);

        // Chống Email Enumeration: Luôn trả về thành công giả lập ngay cả khi email không tồn tại trong hệ thống
        if (account is null)
        {
            return;
        }

        // Vô hiệu hóa các mã OTP cũ còn hiệu lực của user này
        await _unitOfWork.PasswordResetTokens.InvalidateTokensByUserIdAsync(account.UserId, cancellationToken);

        // Sinh mã OTP 6 chữ số ngẫu nhiên cryptographically secure
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        var otpHash = _jwtHelper.HashToken(otp);

        var resetToken = new PasswordResetToken
        {
            PasswordResetTokenId = Guid.NewGuid(),
            UserId = account.UserId,
            TokenHash = otpHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.PasswordResetTokens.AddAsync(resetToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendPasswordResetOtpAsync(account.Email, account.FullName, otp, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var attemptKey = "reset:" + normalizedEmail;
        await EnsureNotLockedAsync(attemptKey, cancellationToken);

        var account = await _unitOfWork.Accounts.GetByEmailWithRoleAsync(normalizedEmail, tracking: true, cancellationToken);

        if (account is null)
        {
            await _attemptLimiter.RecordFailureAsync(attemptKey, cancellationToken);
            throw new ValidationException("Email hoặc mã xác thực OTP không chính xác");
        }

        var otpHash = _jwtHelper.HashToken(request.Otp.Trim());
        var validToken = await _unitOfWork.PasswordResetTokens.GetValidTokenAsync(account.UserId, otpHash, cancellationToken);

        if (validToken is null)
        {
            if (await _attemptLimiter.RecordFailureAsync(attemptKey, cancellationToken))
            {
                // Sai quá nhiều: hủy OTP hiện tại để chặn đoán mò 6 chữ số, buộc xin mã mới.
                await _unitOfWork.PasswordResetTokens.InvalidateTokensByUserIdAsync(account.UserId, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            throw new ValidationException("Mã xác thực OTP không chính xác hoặc đã hết hạn (15 phút)");
        }

        await _attemptLimiter.ResetAsync(attemptKey, cancellationToken);

        // Cập nhật mật khẩu mới
        account.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        account.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Accounts.Update(account);

        // Đánh dấu OTP đã sử dụng
        validToken.UsedAt = DateTime.UtcNow;
        _unitOfWork.PasswordResetTokens.Update(validToken);

        // Thu hồi toàn bộ Refresh Tokens và Access Tokens đang hoạt động của tài khoản này
        var otherRefreshTokens = await _unitOfWork.RefreshTokens.GetOtherActiveTokensAsync(account.UserId, null, tracking: true, cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var rt in otherRefreshTokens)
        {
            rt.RevokedAt = now;
            rt.RevokedReason = "Khôi phục mật khẩu qua Email OTP";
            foreach (var at in rt.AccessTokens.Where(a => a.RevokedAt == null))
            {
                at.RevokedAt = now;
                at.RevokedReason = "Khôi phục mật khẩu qua Email OTP";
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        string subject;
        string email;
        string fullName;
        string? avatarUrl;

        if (_google.AllowUnsignedDevTokens)
        {
            // Chỉ máy dev: đọc mock token KHÔNG kiểm chữ ký (FE chưa cấu hình VITE_GOOGLE_CLIENT_ID).
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            if (!handler.CanReadToken(request.IdToken))
            {
                throw new UnauthorizedException("Google ID Token không hợp lệ hoặc đã hết hạn");
            }

            var jwt = handler.ReadJwtToken(request.IdToken);
            subject = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? Guid.NewGuid().ToString();
            email = (jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? string.Empty).Trim().ToLowerInvariant();
            fullName = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "Google User";
            avatarUrl = jwt.Claims.FirstOrDefault(c => c.Type == "picture")?.Value;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_google.ClientId))
            {
                throw new UnauthorizedException("Đăng nhập Google chưa được cấu hình (GoogleAuth:ClientId)");
            }

            // Google tokeninfo kiểm chữ ký + hạn token; aud và email_verified do mình kiểm.
            using var httpClient = new HttpClient();
            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(
                    $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(request.IdToken)}", cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Không gọi được Google tokeninfo");
                throw new UnauthorizedException("Không xác minh được Google ID Token, vui lòng thử lại");
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new UnauthorizedException("Google ID Token không hợp lệ hoặc đã hết hạn");
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                string? Read(string name) => root.TryGetProperty(name, out var el) ? el.ToString() : null;

                if (!string.Equals(Read("aud"), _google.ClientId, StringComparison.Ordinal))
                {
                    throw new UnauthorizedException("Google ID Token không dành cho ứng dụng này");
                }

                if (!string.Equals(Read("email_verified"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedException("Email Google chưa được xác minh");
                }

                subject = Read("sub") ?? throw new UnauthorizedException("Thiếu Google Subject ID");
                email = (Read("email") ?? string.Empty).Trim().ToLowerInvariant();
                fullName = Read("name") ?? "Google User";
                avatarUrl = Read("picture");
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UnauthorizedException("Tài khoản Google không cung cấp email");
        }

        var externalLogin = await _unitOfWork.UserExternalLogins.GetByProviderAndSubjectAsync("Google", subject, tracking: true, cancellationToken);
        Account? account;

        if (externalLogin != null)
        {
            account = externalLogin.User;
        }
        else
        {
            account = await _unitOfWork.Accounts.GetByEmailWithRoleAsync(email, tracking: true, cancellationToken);

            if (account == null)
            {
                var customerRole = await _unitOfWork.Roles.GetByNameAsync("Customer", tracking: true, cancellationToken)
                    ?? throw new InvalidOperationException("Chưa có role Customer trong bảng roles");

                account = new Account
                {
                    UserId = Guid.NewGuid(),
                    Email = email,
                    FullName = fullName,
                    PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N")),
                    RoleId = customerRole.RoleId,
                    Role = customerRole,
                    Status = AccountStatuses.Active,
                    EmailConfirmedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Accounts.AddAsync(account, cancellationToken);

                var profile = new Profile
                {
                    ProfileId = Guid.NewGuid(),
                    UserId = account.UserId,
                    UserCode = $"RT-{account.UserId:N}"[..10].ToUpperInvariant(),
                    AvatarUrl = avatarUrl,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Profiles.AddAsync(profile, cancellationToken);
                account.Profile = profile;
            }

            var newExtLogin = new UserExternalLogin
            {
                UserExternalLoginId = Guid.NewGuid(),
                UserId = account.UserId,
                Provider = "Google",
                ProviderSubject = subject,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.UserExternalLogins.AddAsync(newExtLogin, cancellationToken);
        }

        if (!string.Equals(account.Status, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException("Tài khoản đã bị vô hiệu hóa");
        }

        account.LastLoginAt = DateTime.UtcNow;
        _unitOfWork.Accounts.Update(account);

        return await IssueTokensAsync(account, Guid.NewGuid(), replaces: null, cancellationToken);
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
                EmailConfirmed = account.EmailConfirmedAt is not null,
                AvatarUrl = account.Profile?.AvatarUrl
            }
        };
    }
}
