using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Quản lý tài khoản, tạo staff, đổi role, khóa và phiên đăng nhập.</summary>
public interface IAdminUserService
{
    Task<PagedResponse<AdminUserResponse>> GetUsersAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<AdminUserDetailResponse> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<AdminUserDetailResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken);
    Task UpdateUserProfileAsync(Guid userId, AdminProfileRequest request, CancellationToken cancellationToken);
    Task ChangeRoleAsync(Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken);
    Task SetUserLockAsync(Guid userId, bool locked, string? reason, CancellationToken cancellationToken);
    /// <summary>Hướng dẫn viên Guide đang hoạt động để chọn khi phân công; Admin và Staff đều gọi được.</summary>
    Task<List<AdminUserResponse>> GetGuidesAsync(CancellationToken cancellationToken);
    Task<List<AdminSessionResponse>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task RevokeSessionsAsync(Guid userId, Guid? sessionId, CancellationToken cancellationToken);
}
