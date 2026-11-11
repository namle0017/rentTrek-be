using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Quản lý tài khoản, tạo staff, đổi role, khóa và phiên đăng nhập.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminUserService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminUserService
{
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    private static AdminUserResponse ToResponse(AdminUserRow x) => new(
        x.UserId, x.UserCode, x.Email, x.FullName, x.Phone, x.RoleName, x.Status, x.AvatarUrl, x.LastLoginAt, x.CreatedAt);

    public async Task<PagedResponse<AdminUserResponse>> GetUsersAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        CheckPaging(request);
        var page = await UnitOfWork.Accounts.GetAdminPagedAsync(new AdminUserFilter
        {
            Search = request.Search,
            Role = request.Role,
            Status = request.Status,
            SortBy = request.SortBy,
            IsDescending = IsDescending(request),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        }, cancellationToken);
        return ToPaged(page, request, ToResponse);
    }

    public async Task<List<AdminUserResponse>> GetGuidesAsync(CancellationToken cancellationToken)
    {
        var rows = await UnitOfWork.Accounts.GetByRolesAsync(
            [RoleNames.Guide, RoleNames.TourGuide], AccountStatuses.Active, cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<AdminUserDetailResponse> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var user = await UnitOfWork.Accounts.GetAdminRowAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy người dùng");
        var profile = await UnitOfWork.Profiles.GetByUserIdAsync(userId, cancellationToken: cancellationToken);
        if (profile?.IsDeleted == true) profile = null;
        var activity = await UnitOfWork.Accounts.GetActivityAsync(userId, cancellationToken);
        return new(ToResponse(user), profile?.DateOfBirth, profile?.Address, profile?.CareerStartDate, profile?.Bio,
            activity.OrderCount, activity.ManagedTripCount, activity.GuidedTripCount);
    }

    public async Task<AdminUserDetailResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var email = request.Email.Trim().ToLowerInvariant();
        var roleName = request.Role;
        var fullName = request.FullName.Trim();
        var userId = Guid.NewGuid();
        await AtomicAsync("account-email:" + email, async () =>
        {
            if (await UnitOfWork.Accounts.ExistsByEmailAsync(email, cancellationToken))
                throw new ConflictException("Email đã được sử dụng");
            var role = await FindRoleAsync(roleName, cancellationToken);
            await UnitOfWork.Accounts.AddAsync(new Account
            {
                UserId = userId,
                Email = email,
                FullName = fullName,
                Phone = request.Phone?.Trim(),
                RoleId = role.RoleId,
                PasswordHash = _passwordHasher.Hash(request.Password!),
                CreatedAt = Now,
                UpdatedAt = Now
            }, cancellationToken);
            await UnitOfWork.Profiles.AddAsync(new Profile
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
            var profile = await UnitOfWork.Profiles.GetByUserIdAsync(userId, tracking: true, cancellationToken);
            if (profile?.IsDeleted == true) profile = null;
            if (profile == null)
            {
                profile = new Profile { ProfileId = Guid.NewGuid(), UserId = userId, UserCode = "USR-" + userId.ToString("N")[..12], CreatedAt = Now };
                await UnitOfWork.Profiles.AddAsync(profile, cancellationToken);
            }
            profile.Address = request.Address?.Trim(); profile.Bio = request.Bio?.Trim();
            profile.DateOfBirth = request.DateOfBirth; profile.CareerStartDate = request.CareerStartDate; profile.UpdatedAt = Now;
            await AuditAsync("Update", "Account", userId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
    }

    private async Task<Account> FindAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await UnitOfWork.Accounts.GetByIdWithRoleAsync(userId, tracking: true, cancellationToken);
        return user is { IsDeleted: false } ? user : throw new NotFoundException("Không tìm thấy người dùng");
    }

    private async Task<Role> FindRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var role = await UnitOfWork.Roles.GetByNameAsync(roleName, cancellationToken: cancellationToken);
        return role is { IsDeleted: false } ? role : throw new BadRequestException("Vai trò chưa được cấu hình");
    }

    public async Task ChangeRoleAsync(Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var roleName = request.Role;
        var reason = request.Reason.Trim();
        await AtomicAsync("account:" + userId, async () =>
        {
            var user = await FindAccountAsync(userId, cancellationToken);
            Check(userId != ActorId && user.Role.RoleName != RoleNames.Admin, "role", "Không được đổi vai trò tài khoản quản trị viên");
            var role = await FindRoleAsync(roleName, cancellationToken);
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
        var tokens = await UnitOfWork.RefreshTokens.GetActiveByUserAsync(userId, Now, cancellationToken);
        return tokens.Select(x => new AdminSessionResponse(x.RefreshTokenId, x.FamilyId, x.CreatedAt, x.ExpiresAt)).ToList();
    }

    public async Task RevokeSessionsAsync(Guid userId, Guid? sessionId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync("account:" + userId, async () =>
        {
            await FindAccountAsync(userId, cancellationToken);
            Guid? familyId = null;
            if (sessionId.HasValue)
                familyId = (await UnitOfWork.RefreshTokens.GetForUserAsync(userId, sessionId.Value, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy phiên đăng nhập")).FamilyId;
            await RevokeTokensAsync(userId, familyId, cancellationToken);
            await AuditAsync("Revoke", "Account", userId, new { sessionId }, cancellationToken);
        }, cancellationToken);
    }

    private async Task RevokeTokensAsync(Guid userId, Guid? familyId, CancellationToken cancellationToken)
    {
        var tokens = await UnitOfWork.RefreshTokens.GetByUserWithAccessTokensAsync(userId, familyId, cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt ??= Now; token.RevokedReason = "Quản trị viên thu hồi phiên";
            foreach (var access in token.AccessTokens) { access.RevokedAt ??= Now; access.RevokedReason = token.RevokedReason; }
        }
    }
}
