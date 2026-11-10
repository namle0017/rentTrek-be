using RenTrek.BLL.DTOs.Users.Requests;
using RenTrek.BLL.DTOs.Users.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ user.
/// </summary>
public class UserService : IUserService
{
    private const long AvatarMaxBytes = 2 * 1024 * 1024;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IFileStorageService _fileStorageService;

    public UserService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IFileStorageService fileStorageService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _fileStorageService = fileStorageService;
    }

    public async Task<UserDetailResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: false, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        var profile = user.Profile ?? await _unitOfWork.Profiles.GetByUserIdAsync(userId, tracking: false, cancellationToken);
        return MapToDetailResponse(user, profile);
    }

    public async Task<UserDetailResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: true, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        user.FullName = request.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Accounts.Update(user);

        var profile = await _unitOfWork.Profiles.GetByUserIdAsync(userId, tracking: true, cancellationToken);
        if (profile is null)
        {
            profile = new Profile
            {
                ProfileId = Guid.NewGuid(),
                UserId = userId,
                UserCode = $"RT-{userId:N}"[..10].ToUpperInvariant(),
                Address = request.Address?.Trim(),
                Bio = request.Bio?.Trim(),
                DateOfBirth = request.DateOfBirth,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Profiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            profile.Address = request.Address?.Trim();
            profile.Bio = request.Bio?.Trim();
            profile.DateOfBirth = request.DateOfBirth;
            profile.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Profiles.Update(profile);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDetailResponse(user, profile);
    }

    public async Task<string> UploadAvatarAsync(
        Guid userId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileLength,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: false, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        var avatarUrl = await _fileStorageService.SaveImageAsync(
            fileStream, originalFileName, contentType, fileLength, "avatars", AvatarMaxBytes, cancellationToken);

        var profile = await _unitOfWork.Profiles.GetByUserIdAsync(userId, tracking: true, cancellationToken);
        if (profile is null)
        {
            profile = new Profile
            {
                ProfileId = Guid.NewGuid(),
                UserId = userId,
                UserCode = $"RT-{userId:N}"[..10].ToUpperInvariant(),
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Profiles.AddAsync(profile, cancellationToken);
        }
        else
        {
            var oldAvatar = profile.AvatarUrl;
            profile.AvatarUrl = avatarUrl;
            profile.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Profiles.Update(profile);

            await _fileStorageService.DeleteImageAsync(oldAvatar, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return avatarUrl;
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? currentJti, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: true, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedException("Mật khẩu hiện tại không chính xác");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Accounts.Update(user);

        // Phương án A: Tự động thu hồi phiên đăng nhập trên các thiết bị khác, giữ lại thiết bị hiện tại
        var currentFamilyId = await GetCurrentFamilyIdAsync(currentJti, cancellationToken);
        var otherRefreshTokens = await _unitOfWork.RefreshTokens.GetOtherActiveTokensAsync(
            userId, currentFamilyId, tracking: true, cancellationToken);
        Revoke(otherRefreshTokens, "Đổi mật khẩu - thu hồi phiên làm việc khác");

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<MySessionResponse>> GetSessionsAsync(Guid userId, string? currentJti, CancellationToken cancellationToken = default)
    {
        var currentFamilyId = await GetCurrentFamilyIdAsync(currentJti, cancellationToken);
        // Token mới nhất trước, nên phần tử đầu mỗi nhóm là token đang dùng của family.
        var latest = (await _unitOfWork.RefreshTokens.GetActiveByUserAsync(userId, DateTime.UtcNow, cancellationToken))
            .GroupBy(x => x.FamilyId).Select(g => g.First()).ToList();
        var startedAt = await _unitOfWork.RefreshTokens.GetFamilyStartTimesAsync(
            userId, latest.Select(x => x.FamilyId).ToList(), cancellationToken);

        return latest
            .Select(x => new MySessionResponse(x.FamilyId, startedAt.GetValueOrDefault(x.FamilyId, x.CreatedAt),
                x.CreatedAt, x.ExpiresAt, x.FamilyId == currentFamilyId))
            .OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.LastActiveAt)
            .ToList();
    }

    public async Task RevokeSessionAsync(Guid userId, Guid familyId, string? currentJti, CancellationToken cancellationToken = default)
    {
        if (familyId == await GetCurrentFamilyIdAsync(currentJti, cancellationToken))
        {
            throw new ValidationException("Không thể thu hồi phiên hiện tại, hãy dùng đăng xuất");
        }

        var tokens = await _unitOfWork.RefreshTokens.GetByUserWithAccessTokensAsync(userId, familyId, cancellationToken);
        var now = DateTime.UtcNow;
        if (!tokens.Any(x => x.RevokedAt == null && x.ExpiresAt > now))
        {
            throw new NotFoundException("Không tìm thấy phiên đăng nhập đang hoạt động");
        }

        Revoke(tokens, "Người dùng đăng xuất phiên khác");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RevokeOtherSessionsAsync(Guid userId, string? currentJti, CancellationToken cancellationToken = default)
    {
        // Không xác định được phiên hiện tại thì không thu hồi, tránh đăng xuất luôn chính mình.
        var currentFamilyId = await GetCurrentFamilyIdAsync(currentJti, cancellationToken)
            ?? throw new UnauthorizedException("Không xác định được phiên hiện tại");

        var tokens = await _unitOfWork.RefreshTokens.GetOtherActiveTokensAsync(userId, currentFamilyId, tracking: true, cancellationToken);
        var now = DateTime.UtcNow;
        var count = tokens.Where(x => x.ExpiresAt > now).Select(x => x.FamilyId).Distinct().Count();

        Revoke(tokens, "Người dùng đăng xuất các phiên khác");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<Guid?> GetCurrentFamilyIdAsync(string? currentJti, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentJti)) return null;
        var accessToken = await _unitOfWork.AccessTokens.GetByJtiWithRefreshTokenAsync(currentJti, cancellationToken);
        return accessToken?.RefreshToken?.FamilyId;
    }

    /// <summary>Thu hồi các refresh token (đã tracking, kèm AccessTokens) chưa thu hồi cùng access token của chúng.</summary>
    private static void Revoke(IEnumerable<RefreshToken> refreshTokens, string reason)
    {
        var now = DateTime.UtcNow;
        foreach (var rt in refreshTokens)
        {
            if (rt.RevokedAt == null)
            {
                rt.RevokedAt = now;
                rt.RevokedReason = reason;
            }

            foreach (var at in rt.AccessTokens.Where(a => a.RevokedAt == null))
            {
                at.RevokedAt = now;
                at.RevokedReason = reason;
            }
        }
    }

    private static UserDetailResponse MapToDetailResponse(Account user, Profile? profile)
    {
        return new UserDetailResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role?.RoleName ?? "Customer",
            Status = user.Status,
            EmailConfirmed = user.EmailConfirmedAt is not null,
            AvatarUrl = profile?.AvatarUrl ?? user.Profile?.AvatarUrl,
            Address = profile?.Address ?? user.Profile?.Address,
            Bio = profile?.Bio ?? user.Profile?.Bio,
            DateOfBirth = profile?.DateOfBirth ?? user.Profile?.DateOfBirth,
            UserCode = profile?.UserCode ?? user.Profile?.UserCode,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
