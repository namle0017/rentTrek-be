using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Users;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDetailResponse> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        return MapToDetailResponse(user);
    }

    public async Task<UserDetailResponse> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .QueryTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        user.FullName = request.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<User>().Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDetailResponse(user);
    }

    public async Task ChangePasswordAsync(long userId, ChangePasswordRequest request, string? currentJti, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .QueryTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

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
        _unitOfWork.Repository<User>().Update(user);

        // Phương án A: Tự động thu hồi phiên đăng nhập trên các thiết bị khác, giữ lại thiết bị hiện tại
        string? currentFamilyId = null;
        if (!string.IsNullOrWhiteSpace(currentJti))
        {
            var currentAccessToken = await _unitOfWork.Repository<AccessToken>()
                .Query()
                .Include(at => at.RefreshToken)
                .FirstOrDefaultAsync(at => at.Jti == currentJti, cancellationToken);

            currentFamilyId = currentAccessToken?.RefreshToken?.FamilyId;
        }

        var otherRefreshTokens = await _unitOfWork.Repository<RefreshToken>()
            .QueryTracking()
            .Include(rt => rt.AccessTokens)
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && (currentFamilyId == null || rt.FamilyId != currentFamilyId))
            .ToListAsync(cancellationToken);

        foreach (var rt in otherRefreshTokens)
        {
            rt.RevokedAt = DateTime.UtcNow;
            rt.RevokedReason = "Đổi mật khẩu - thu hồi phiên làm việc khác";

            foreach (var at in rt.AccessTokens.Where(a => a.RevokedAt == null))
            {
                at.RevokedAt = DateTime.UtcNow;
                at.RevokedReason = "Đổi mật khẩu - thu hồi phiên làm việc khác";
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UserSessionResponse>> GetActiveSessionsAsync(long userId, string? currentJti, CancellationToken cancellationToken = default)
    {
        string? currentFamilyId = null;
        if (!string.IsNullOrWhiteSpace(currentJti))
        {
            var currentAccessToken = await _unitOfWork.Repository<AccessToken>()
                .Query()
                .Include(at => at.RefreshToken)
                .FirstOrDefaultAsync(at => at.Jti == currentJti, cancellationToken);

            currentFamilyId = currentAccessToken?.RefreshToken?.FamilyId;
        }

        var activeTokens = await _unitOfWork.Repository<RefreshToken>()
            .Query()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync(cancellationToken);

        var sessions = activeTokens
            .GroupBy(rt => rt.FamilyId)
            .Select(g =>
            {
                var latest = g.First();
                var earliest = g.Last();
                return new UserSessionResponse
                {
                    FamilyId = g.Key,
                    DeviceInfo = string.IsNullOrWhiteSpace(latest.DeviceInfo) ? "Trình duyệt web" : latest.DeviceInfo,
                    IpAddress = latest.IpAddress,
                    CreatedAt = earliest.CreatedAt,
                    LastActiveAt = latest.CreatedAt,
                    ExpiresAt = latest.ExpiresAt,
                    IsCurrent = !string.IsNullOrEmpty(currentFamilyId) && g.Key == currentFamilyId
                };
            })
            .OrderByDescending(s => s.IsCurrent)
            .ThenByDescending(s => s.LastActiveAt)
            .ToList();

        return sessions;
    }

    public async Task RevokeSessionAsync(long userId, string familyId, string? currentJti, CancellationToken cancellationToken = default)
    {
        var tokens = await _unitOfWork.Repository<RefreshToken>()
            .QueryTracking()
            .Include(rt => rt.AccessTokens)
            .Where(rt => rt.UserId == userId && rt.FamilyId == familyId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            throw new NotFoundException("Phiên làm việc không tồn tại hoặc đã kết thúc");
        }

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "Người dùng thu hồi phiên làm việc";

            foreach (var at in token.AccessTokens.Where(a => a.RevokedAt == null))
            {
                at.RevokedAt = DateTime.UtcNow;
                at.RevokedReason = "Người dùng thu hồi phiên làm việc";
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsAsync(long userId, string? currentJti, CancellationToken cancellationToken = default)
    {
        string? currentFamilyId = null;
        if (!string.IsNullOrWhiteSpace(currentJti))
        {
            var currentAccessToken = await _unitOfWork.Repository<AccessToken>()
                .Query()
                .Include(at => at.RefreshToken)
                .FirstOrDefaultAsync(at => at.Jti == currentJti, cancellationToken);

            currentFamilyId = currentAccessToken?.RefreshToken?.FamilyId;
        }

        if (string.IsNullOrWhiteSpace(currentFamilyId))
        {
            throw new UnauthorizedException("Không thể xác định phiên làm việc hiện tại");
        }

        var otherTokens = await _unitOfWork.Repository<RefreshToken>()
            .QueryTracking()
            .Include(rt => rt.AccessTokens)
            .Where(rt => rt.UserId == userId && rt.FamilyId != currentFamilyId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in otherTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "Đăng xuất khỏi tất cả các thiết bị khác";

            foreach (var at in token.AccessTokens.Where(a => a.RevokedAt == null))
            {
                at.RevokedAt = DateTime.UtcNow;
                at.RevokedReason = "Đăng xuất khỏi tất cả các thiết bị khác";
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static UserDetailResponse MapToDetailResponse(User user)
    {
        return new UserDetailResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role?.RoleName ?? "Customer",
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
