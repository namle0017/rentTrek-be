using RenTrek.BLL.DTOs.Users.Requests;
using RenTrek.BLL.DTOs.Users.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IUserService
{
    Task<UserDetailResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDetailResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<string> UploadAvatarAsync(Guid userId, Stream fileStream, string originalFileName, string contentType, long fileLength, CancellationToken cancellationToken = default);
    Task SendChangePasswordOtpAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? currentJti, CancellationToken cancellationToken = default);

    /// <summary>Phiên còn hiệu lực của chính mình (mỗi family refresh token là một phiên), phiên hiện tại đứng đầu.</summary>
    Task<List<MySessionResponse>> GetSessionsAsync(Guid userId, string? currentJti, CancellationToken cancellationToken = default);

    /// <summary>Đăng xuất một phiên khác. Không thu hồi phiên hiện tại (dùng /auth/logout).</summary>
    Task RevokeSessionAsync(Guid userId, Guid familyId, string? currentJti, CancellationToken cancellationToken = default);

    /// <summary>Đăng xuất mọi phiên trừ phiên hiện tại; trả về số phiên đã thu hồi.</summary>
    Task<int> RevokeOtherSessionsAsync(Guid userId, string? currentJti, CancellationToken cancellationToken = default);
}
