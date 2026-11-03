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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDetailResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: false, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        return MapToDetailResponse(user);
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDetailResponse(user);
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
        Guid? currentFamilyId = null;
        if (!string.IsNullOrWhiteSpace(currentJti))
        {
            var currentAccessToken = await _unitOfWork.AccessTokens.GetByJtiWithRefreshTokenAsync(currentJti, cancellationToken);
            currentFamilyId = currentAccessToken?.RefreshToken?.FamilyId;
        }

        var otherRefreshTokens = await _unitOfWork.RefreshTokens.GetOtherActiveTokensAsync(
            userId, currentFamilyId, tracking: true, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var rt in otherRefreshTokens)
        {
            rt.RevokedAt = now;
            rt.RevokedReason = "Đổi mật khẩu - thu hồi phiên làm việc khác";

            foreach (var at in rt.AccessTokens.Where(a => a.RevokedAt == null))
            {
                at.RevokedAt = now;
                at.RevokedReason = "Đổi mật khẩu - thu hồi phiên làm việc khác";
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static UserDetailResponse MapToDetailResponse(Account user)
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
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
