using RenTrek.BLL.DTOs.Users;

namespace RenTrek.BLL.Services.Interfaces;

public interface IUserService
{
    Task<UserDetailResponse> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserDetailResponse> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequest request, string? currentJti, CancellationToken cancellationToken = default);
    Task<List<UserSessionResponse>> GetActiveSessionsAsync(long userId, string? currentJti, CancellationToken cancellationToken = default);
    Task RevokeSessionAsync(long userId, string familyId, string? currentJti, CancellationToken cancellationToken = default);
    Task RevokeOtherSessionsAsync(long userId, string? currentJti, CancellationToken cancellationToken = default);
}
