using RenTrek.BLL.DTOs.Users;

namespace RenTrek.BLL.Services.Interfaces;

public interface IUserService
{
    Task<UserDetailResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDetailResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<string> UploadAvatarAsync(Guid userId, Stream fileStream, string originalFileName, string contentType, long fileLength, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? currentJti, CancellationToken cancellationToken = default);
}
