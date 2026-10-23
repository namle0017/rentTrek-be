using RenTrek.BLL.DTOs.Users;

namespace RenTrek.BLL.Services.Interfaces;

public interface IUserService
{
    Task<UserDetailResponse> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserDetailResponse> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
}
