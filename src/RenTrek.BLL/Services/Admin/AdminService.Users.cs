using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Admin;

public sealed partial class AdminService
{
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
        var email = Required(request.Email, "email", 255).ToLowerInvariant();
        Check(System.Net.Mail.MailAddress.TryCreate(email, out var address) && address.Address == email, "email", "Email không hợp lệ");
        var roleName = Choice(request.Role, "role", "Staff", "Guide");
        var password = request.Password;
        Check(!string.IsNullOrEmpty(password) && password.Length is >= 8 and <= 128 && password.Any(char.IsUpper)
            && password.Any(char.IsLower) && password.Any(char.IsDigit) && password.Any(c => !char.IsLetterOrDigit(c)),
            "password", "Mật khẩu 8–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt");
        var fullName = Required(request.FullName, "fullName", 100);
        ValidatePhone(request.Phone, "phone");
        Check(request.CareerStartDate == null || request.CareerStartDate <= DateOnly.FromDateTime(Now), "careerStartDate", "Ngày vào nghề không được ở tương lai");
        var userId = Guid.NewGuid();
        await AtomicAsync("account-email:" + email, async () =>
        {
            if (await _unitOfWork.Accounts.Query().IgnoreQueryFilters().AnyAsync(x => x.Email.ToLower() == email, cancellationToken))
                throw new ConflictException("Email đã được sử dụng");
            var role = await _unitOfWork.Roles.Query().SingleOrDefaultAsync(x => x.RoleName == roleName && !x.IsDeleted, cancellationToken)
                ?? throw new BadRequestException("Vai trò chưa được cấu hình");
            await _unitOfWork.Accounts.AddAsync(new Account { UserId = userId, Email = email, FullName = fullName,
                Phone = request.Phone?.Trim(), RoleId = role.RoleId, PasswordHash = _passwordHasher.Hash(password!),
                CreatedAt = Now, UpdatedAt = Now }, cancellationToken);
            await _unitOfWork.Profiles.AddAsync(new Profile { ProfileId = Guid.NewGuid(), UserId = userId,
                UserCode = (roleName == "Guide" ? "GUI-" : "STA-") + userId.ToString("N")[..12].ToUpperInvariant(),
                CareerStartDate = request.CareerStartDate, CreatedAt = Now, UpdatedAt = Now }, cancellationToken);
            await AuditAsync("Create", "Account", userId, new { email, role = roleName }, cancellationToken);
        }, cancellationToken);
        return await GetUserAsync(userId, cancellationToken);
    }

    private static void ValidatePhone(string? phone, string field)
        => Check(string.IsNullOrWhiteSpace(phone) || System.Text.RegularExpressions.Regex.IsMatch(phone.Trim(), @"^(03|05|07|08|09)\d{8}$"), field, "Số điện thoại không hợp lệ");

    public async Task UpdateUserProfileAsync(Guid userId, AdminProfileRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var fullName = Required(request.FullName, "fullName", 100);
        ValidatePhone(request.Phone, "phone");
        Check(request.DateOfBirth == null || request.DateOfBirth <= DateOnly.FromDateTime(Now), "dateOfBirth", "Ngày sinh không hợp lệ");
        Check(request.CareerStartDate == null || request.CareerStartDate <= DateOnly.FromDateTime(Now), "careerStartDate", "Ngày vào nghề không được ở tương lai");
        Check(request.Address == null || request.Address.Trim().Length <= 500, "address", "Địa chỉ tối đa 500 ký tự");
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
        var roleName = Choice(request.Role, "role", "Customer", "Staff", "Guide");
        var reason = Required(request.Reason, "reason", 500);
        await AtomicAsync("account:" + userId, async () =>
        {
            var user = await FindAccountAsync(userId, cancellationToken);
            Check(userId != ActorId && user.Role.RoleName != "Admin", "role", "Không được đổi vai trò tài khoản quản trị viên");
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
            Check(userId != ActorId && user.Role.RoleName != "Admin", "userId", "Không được khóa tài khoản quản trị viên");
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

    public async Task<PagedResponse<AdminAuditResponse>> GetAuditLogsAsync(AdminQueryRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var query = _unitOfWork.AuditLogs.Query();
        if (userId.HasValue) query = query.Where(x => x.ActorUserId == userId || (x.EntityType == "Account" && x.EntityId == userId));
        if (request.ActorUserId.HasValue) query = query.Where(x => x.ActorUserId == request.ActorUserId);
        if (request.EntityId.HasValue) query = query.Where(x => x.EntityId == request.EntityId);
        if (request.EntityType != null) query = query.Where(x => x.EntityType == request.EntityType);
        if (request.Action != null) query = query.Where(x => x.Action == request.Action);
        if (request.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= request.FromDate);
        if (request.ToDate.HasValue) query = query.Where(x => x.CreatedAt <= request.ToDate);
        return await PageAsync(query, request, x => new AdminAuditResponse(x.AuditLogId, x.ActorUserId,
            x.ActorUser == null ? null : x.ActorUser.FullName, x.Action, x.EntityType, x.EntityId,
            AuditDetails(x.Details), x.CreatedAt), cancellationToken);
    }
}
