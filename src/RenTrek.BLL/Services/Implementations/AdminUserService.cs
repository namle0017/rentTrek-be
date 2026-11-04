using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Quản lý tài khoản, tạo staff, đổi role, khóa và phiên đăng nhập.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminUserService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminUserService
{
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    private static readonly Expression<Func<Account, AdminUserResponse>> UserProjection = user => new(
        user.UserId, user.Profile == null ? null : user.Profile.UserCode, user.Email, user.FullName,
        user.Phone, user.Role.RoleName, user.Status, user.Profile == null ? null : user.Profile.AvatarUrl,
        user.LastLoginAt, user.CreatedAt);

    public Task<PagedResponse<AdminUserResponse>> GetUsersAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var query = _unitOfWork.Accounts.Query().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(search) || x.Email.ToLower().Contains(search)
                || (x.Phone != null && x.Phone.Contains(search)) || (x.Profile != null && x.Profile.UserCode.ToLower().Contains(search)));
        }
        if (request.Role != null) query = query.Where(x => x.Role.RoleName == request.Role);
        if (request.Status != null) query = query.Where(x => x.Status == request.Status);
        return PageAsync(query, request, UserProjection, cancellationToken, "CreatedAt", "FullName", "Email", "Status");
    }

    public async Task<AdminUserDetailResponse> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var user = await _unitOfWork.Accounts.Query().Where(x => !x.IsDeleted && x.UserId == userId)
            .Select(UserProjection).SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Không tìm thấy người dùng");
        var profile = await _unitOfWork.Profiles.Query().SingleOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, cancellationToken);
        return new(user, profile?.DateOfBirth, profile?.Address, profile?.CareerStartDate, profile?.Bio,
            await _unitOfWork.Orders.Query().CountAsync(x => x.CustomerUserId == userId, cancellationToken),
            await _unitOfWork.Trips.Query().CountAsync(x => !x.IsDeleted && x.ManagedByUserId == userId, cancellationToken),
            await _unitOfWork.TripGroups.Query().Where(x => !x.IsDeleted && x.GuideUserId == userId)
                .Select(x => x.TripId).Distinct().CountAsync(cancellationToken));
    }

    public async Task<AdminUserDetailResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var email = request.Email.Trim().ToLowerInvariant();
        var roleName = request.Role;
        var password = request.Password;
        var fullName = request.FullName.Trim();
        var userId = Guid.NewGuid();
        await AtomicAsync("account-email:" + email, async () =>
        {
            if (await _unitOfWork.Accounts.Query().IgnoreQueryFilters().AnyAsync(x => x.Email.ToLower() == email, cancellationToken))
                throw new ConflictException("Email đã được sử dụng");
            var role = await _unitOfWork.Roles.Query().SingleOrDefaultAsync(x => x.RoleName == roleName && !x.IsDeleted, cancellationToken)
                ?? throw new BadRequestException("Vai trò chưa được cấu hình");
            await _unitOfWork.Accounts.AddAsync(new Account
            {
                UserId = userId,
                Email = email,
                FullName = fullName,
                Phone = request.Phone?.Trim(),
                RoleId = role.RoleId,
                PasswordHash = _passwordHasher.Hash(password!),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await _unitOfWork.Profiles.AddAsync(new Profile
            {
                ProfileId = Guid.NewGuid(),
                UserId = userId,
                UserCode = (roleName == RoleNames.Guide ? "GUI-" : "STA-") + userId.ToString("N")[..12].ToUpperInvariant(),
                CareerStartDate = request.CareerStartDate,
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await AuditAsync("Create", "Account", userId, new { email, role = roleName }, cancellationToken);
        }, cancellationToken);
        return await GetUserAsync(userId, cancellationToken);
    }

    public async Task UpdateUserProfileAsync(Guid userId, AdminProfileRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var fullName = request.FullName.Trim();
        await AtomicAsync("account:" + userId, async () =>
        {
            var user = await FindAccountAsync(userId, cancellationToken);
            var before = new { user.FullName, user.Phone };
            user.FullName = fullName; user.Phone = request.Phone?.Trim(); user.UpdatedAt = Now;
            var profile = await _unitOfWork.Profiles.QueryTracking().SingleOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, cancellationToken);
            if (profile == null)
            {
                profile = new Profile { ProfileId = Guid.NewGuid(), UserId = userId, UserCode = "USR-" + userId.ToString("N")[..12], CreatedAt = Now };
                await _unitOfWork.Profiles.AddAsync(profile, cancellationToken);
            }
            profile.Address = request.Address?.Trim(); profile.Bio = request.Bio?.Trim();
            profile.DateOfBirth = request.DateOfBirth; profile.CareerStartDate = request.CareerStartDate; profile.UpdatedAt = Now;
            await AuditAsync("Update", "Account", userId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
    }

    private async Task<Account> FindAccountAsync(Guid userId, CancellationToken cancellationToken)
        => await _unitOfWork.Accounts.QueryTracking().Include(x => x.Role)
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy người dùng");

    public async Task ChangeRoleAsync(Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var roleName = request.Role;
        var reason = request.Reason.Trim();
        await AtomicAsync("account:" + userId, async () =>
        {
            var user = await FindAccountAsync(userId, cancellationToken);
            Check(userId != ActorId && user.Role.RoleName != RoleNames.Admin, "role", "Không được đổi vai trò tài khoản quản trị viên");
            var role = await _unitOfWork.Roles.Query().SingleOrDefaultAsync(x => !x.IsDeleted && x.RoleName == roleName, cancellationToken)
                ?? throw new BadRequestException("Vai trò chưa được cấu hình");
            var before = user.Role.RoleName;
            user.RoleId = role.RoleId; user.UpdatedAt = Now;
            await RevokeTokensAsync(userId, null, cancellationToken);
            await AuditAsync("Update", "Account", userId, new { before, after = roleName, reason }, cancellationToken);
        }, cancellationToken);
    }

    public async Task SetUserLockAsync(Guid userId, bool locked, string? reason, CancellationToken cancellationToken)
    {
        RequireAdmin();
        if (locked) reason = Required(reason, "reason", 500);
        await AtomicAsync("account:" + userId, async () =>
        {
            var user = await FindAccountAsync(userId, cancellationToken);
            Check(userId != ActorId && user.Role.RoleName != RoleNames.Admin, "userId", "Không được khóa tài khoản quản trị viên");
            var before = user.Status;
            user.Status = locked ? "Locked" : "Active"; user.UpdatedAt = Now;
            if (locked) await RevokeTokensAsync(userId, null, cancellationToken);
            await AuditAsync(locked ? "Lock" : "Unlock", "Account", userId, new { before, after = user.Status, reason }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<List<AdminSessionResponse>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await FindAccountAsync(userId, cancellationToken);
        return await _unitOfWork.RefreshTokens.Query().Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > Now)
            .OrderByDescending(x => x.CreatedAt).Select(x => new AdminSessionResponse(x.RefreshTokenId, x.FamilyId, x.CreatedAt, x.ExpiresAt)).ToListAsync(cancellationToken);
    }

    public async Task RevokeSessionsAsync(Guid userId, Guid? sessionId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync("account:" + userId, async () =>
        {
            await FindAccountAsync(userId, cancellationToken);
            Guid? familyId = null;
            if (sessionId.HasValue)
                familyId = (await _unitOfWork.RefreshTokens.Query().SingleOrDefaultAsync(x => x.UserId == userId && x.RefreshTokenId == sessionId, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy phiên đăng nhập")).FamilyId;
            await RevokeTokensAsync(userId, familyId, cancellationToken);
            await AuditAsync("Revoke", "Account", userId, new { sessionId }, cancellationToken);
        }, cancellationToken);
    }

    private async Task RevokeTokensAsync(Guid userId, Guid? familyId, CancellationToken cancellationToken)
    {
        var tokens = await _unitOfWork.RefreshTokens.QueryTracking().Include(x => x.AccessTokens)
            .Where(x => x.UserId == userId && (!familyId.HasValue || x.FamilyId == familyId)).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt ??= Now; token.RevokedReason = "Quản trị viên thu hồi phiên";
            foreach (var access in token.AccessTokens) { access.RevokedAt ??= Now; access.RevokedReason = token.RevokedReason; }
        }
    }
}
